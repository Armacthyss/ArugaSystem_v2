# Refreshes the demo data on the live database (185.194.217.224).
#
#   .\seed-demo.ps1           back up the database, then run backend\Database\DemoSeed.sql
#   .\seed-demo.ps1 -Restore  put the database back as it was before the last seed
#   .\seed-demo.ps1 -At "2026-10-04 06:00"
#                             have the server do the same by itself at that time
#                             (server time = Manila time), e.g. before a morning demo
#
# DemoSeed only touches the demo records (IDs starting A2A, batches DEMO-) and
# computes ages, schedules and today's queue from the day it runs, so run it
# on the morning of a demo. On a day that isn't a vaccination day, first open
# the day under Admin > Operating Hours (exception: open), then run this.
#
# The database runs in Docker (container aruga-sql); its password is read
# inside the container, so it never has to be typed here.

param([switch]$Restore, [string]$At)

$ErrorActionPreference = 'Stop'
$server = 'root@185.194.217.224'
Set-Location $PSScriptRoot

# Runs inside the database container. $1 = seed | restore
$inner = @'
set -e
S=$(ls /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd 2>/dev/null | head -1)
P="${MSSQL_SA_PASSWORD:-$SA_PASSWORD}"
OPTS="-S localhost -U sa -b"
case "$S" in *tools18*) OPTS="$OPTS -C";; esac
BAK=/var/opt/mssql/data/ArugaSystemDB_before_demo.bak
if [ "$1" = restore ]; then
  test -f $BAK || { echo "No backup found."; exit 1; }
  "$S" $OPTS -P "$P" -Q "ALTER DATABASE ArugaSystemDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE ArugaSystemDB FROM DISK=N'$BAK' WITH REPLACE; ALTER DATABASE ArugaSystemDB SET MULTI_USER;"
  echo "Database restored from the backup."
else
  "$S" $OPTS -P "$P" -Q "BACKUP DATABASE ArugaSystemDB TO DISK=N'$BAK' WITH INIT" > /dev/null
  echo "Backup saved."
  "$S" $OPTS -P "$P" -I -f 65001 -i /tmp/DemoSeed.sql
fi
'@

# Runs on the server: copy the files into the container and run the step above
$outer = @'
set -e
docker cp /tmp/aruga-seed-inner.sh aruga-sql:/tmp/aruga-seed-inner.sh
[ -f /tmp/DemoSeed.sql ] && docker cp /tmp/DemoSeed.sql aruga-sql:/tmp/DemoSeed.sql
[ "$1" = restore ] && systemctl stop aruga-api
docker exec aruga-sql bash /tmp/aruga-seed-inner.sh "$1" || STATUS=$?
[ "$1" = restore ] && systemctl start aruga-api
rm -f /tmp/DemoSeed.sql /tmp/aruga-seed-inner.sh
exit ${STATUS:-0}
'@

if ($At) {
    # Keep the files on the server and let systemd run them once at $At.
    # The scheduled run keeps its own backup (..._before_scheduled_seed.bak),
    # so the backup from a manual seed isn't overwritten.
    $when = [datetime]::ParseExact($At, 'yyyy-MM-dd HH:mm', $null).ToString('yyyy-MM-dd HH:mm:00')
    $schedule = @'
set -e
mkdir -p /root/aruga-demo
mv /tmp/aruga-seed-inner.sh /tmp/DemoSeed.sql /root/aruga-demo/
sed -i 's#ArugaSystemDB_before_demo.bak#ArugaSystemDB_before_scheduled_seed.bak#' /root/aruga-demo/aruga-seed-inner.sh
cat > /root/aruga-demo/reseed.sh <<'EOS'
#!/bin/bash
{ echo "== $(date)"
  docker cp /root/aruga-demo/aruga-seed-inner.sh aruga-sql:/tmp/aruga-seed-inner.sh
  docker cp /root/aruga-demo/DemoSeed.sql aruga-sql:/tmp/DemoSeed.sql
  docker exec aruga-sql bash /tmp/aruga-seed-inner.sh seed
  echo "exit: $?"; } >> /root/aruga-demo/reseed.log 2>&1
EOS
systemctl stop aruga-demo-reseed.timer 2>/dev/null || true
systemctl reset-failed aruga-demo-reseed.service aruga-demo-reseed.timer 2>/dev/null || true
systemd-run --unit=aruga-demo-reseed --on-calendar="WHEN" --timer-property=AccuracySec=1s /bin/bash /root/aruga-demo/reseed.sh
systemctl list-timers aruga-demo-reseed.timer --no-pager | head -2
'@
    $schedule = $schedule.Replace('WHEN', $when)
}

$tmp = Join-Path $env:TEMP 'aruga-seed'
New-Item -ItemType Directory -Force $tmp | Out-Null
# Unix line endings, or bash on the server can't read them
[IO.File]::WriteAllText("$tmp\aruga-seed-inner.sh", $inner.Replace("`r`n", "`n"))
$main = if ($At) { $schedule } else { $outer }
[IO.File]::WriteAllText("$tmp\aruga-seed.sh", $main.Replace("`r`n", "`n"))

$files = @("$tmp\aruga-seed-inner.sh", "$tmp\aruga-seed.sh")
if (-not $Restore) { $files += 'backend\Database\DemoSeed.sql' }

Write-Host "`n== Uploading" -ForegroundColor Cyan
scp @files "${server}:/tmp/"
if ($LASTEXITCODE -ne 0) { Write-Host 'Upload failed, nothing was changed.' -ForegroundColor Red; exit 1 }

$mode = if ($Restore) { 'restore' } elseif ($At) { "schedule for $At" } else { 'seed' }
Write-Host "`n== Running on the server ($mode)" -ForegroundColor Cyan
ssh $server "bash /tmp/aruga-seed.sh $(if ($Restore) { 'restore' } else { 'seed' }); S=`$?; rm -f /tmp/aruga-seed.sh; exit `$S"

if ($LASTEXITCODE -eq 0 -and $At) {
    Write-Host "`nDone: the server will refresh the demo data at $At." -ForegroundColor Green
    Write-Host 'Afterwards, check it worked: ssh root@185.194.217.224 "tail -5 /root/aruga-demo/reseed.log"'
} elseif ($LASTEXITCODE -eq 0) {
    Write-Host "`nDone." -ForegroundColor Green
} else {
    Write-Host "`nSomething went wrong (see above). Nothing was changed if the backup step failed;" -ForegroundColor Red
    Write-Host "otherwise run .\seed-demo.ps1 -Restore to put the database back." -ForegroundColor Red
    exit 1
}
