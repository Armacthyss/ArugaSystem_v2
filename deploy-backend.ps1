# Updates the live backend (185.194.217.224) with the code on this computer.
#
#   .\deploy-backend.ps1            build, upload, install, restart, check
#   .\deploy-backend.ps1 -Rollback  put back the version from before the last update
#
# Run it from PowerShell in this folder (ArugaSystem). It asks for the server
# password twice (upload + install) unless an SSH key is set up; see
# backend/README.md "Updating the live server".
#
# The server keeps its own appsettings.json (Gmail, TextBee, database), so
# this computer's settings files are never uploaded.

param([switch]$Rollback)

$ErrorActionPreference = 'Stop'
$server  = 'root@185.194.217.224'
$appDir  = '/opt/aruga-api'
$service = 'aruga-api'
Set-Location $PSScriptRoot

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

if ($Rollback) {
    Step 'Putting back the previous version on the server'
    ssh $server "test -d $appDir-previous && cp -a $appDir-previous/. $appDir/ && systemctl restart $service && sleep 3 && systemctl is-active $service"
    if ($LASTEXITCODE -ne 0) { Write-Host 'Rollback failed (no previous version saved yet?).' -ForegroundColor Red; exit 1 }
    Write-Host 'Done: the previous version is running again.' -ForegroundColor Green
    exit 0
}

Step 'Building the backend'
dotnet publish backend\AndroidWebAPI.csproj -c Release -o publish-api -v q -nologo
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed, nothing was uploaded.' -ForegroundColor Red; exit 1 }

Step 'Packing (without this computer''s settings files)'
tar -czf aruga-api.tgz --exclude=appsettings.json --exclude=appsettings.Development.json -C publish-api .
if ($LASTEXITCODE -ne 0) { exit 1 }
$localDll = (Get-FileHash publish-api\AndroidWebAPI.dll -Algorithm MD5).Hash.ToLower()

Step 'Uploading to the server'
scp aruga-api.tgz "${server}:/tmp/aruga-api.tgz"
if ($LASTEXITCODE -ne 0) { Write-Host 'Upload failed, the server was not changed.' -ForegroundColor Red; exit 1 }

Step 'Installing and restarting (the current version is kept for -Rollback)'
$remote = "set -e; rm -rf $appDir-previous; cp -a $appDir $appDir-previous; " +
          "tar -xzf /tmp/aruga-api.tgz -C $appDir; rm -f /tmp/aruga-api.tgz; " +
          "systemctl restart $service; sleep 3; systemctl is-active $service; md5sum $appDir/AndroidWebAPI.dll"
$out = ssh $server $remote
$out | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -eq 0 -and ($out -join "`n") -match 'active' -and ($out -join "`n") -match $localDll) {
    Write-Host "`nDone: the live backend is updated and running." -ForegroundColor Green
} else {
    Write-Host "`nSomething went wrong. Run .\deploy-backend.ps1 -Rollback to put the previous version back." -ForegroundColor Red
    exit 1
}
