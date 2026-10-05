using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AndroidWebAPI.Services
{
    // Survey mode: a public test copy of Aruga that cleans itself up.
    //
    // While it's on, the whole database is put back to a saved clean copy
    // (the "baseline") once nobody is using the system any more, so the next
    // person who tries the test accounts starts fresh. Several testers can be
    // on at the same time: the reset waits until every one of them has
    // logged out or gone idle, so nobody's work disappears mid-test.
    //
    // Switched on and off from the server with .\survey-mode.ps1 (it saves the
    // baseline and sets SurveyMode__Enabled for the aruga-api service), never
    // from the website, since the test accounts are shared with the public.
    //
    //   "SurveyMode": {
    //     "Enabled": false,
    //     "BaselineBackup": "/var/opt/mssql/data/ArugaSystemDB_survey_baseline.bak",
    //     "IdleMinutes": 10,      a tester with no request for this long has left
    //     "QuietMinutes": 30,     ...or with no change for this long (an open tab keeps polling)
    //     "GraceMinutes": 2,      wait after the last logout (time to log in as another role)
    //     "RunDemoSeed": true,    refresh the demo families' dates to today after a reset
    //     "SendMessages": false   real email/SMS stay off: testers can't text real people
    //   }
    public class SurveyMode
    {
        private readonly IConfiguration _config;

        public SurveyMode(IConfiguration config) => _config = config;

        public bool Enabled => _config.GetValue("SurveyMode:Enabled", false);
        public bool SendMessages => _config.GetValue("SurveyMode:SendMessages", false);
        public bool RunDemoSeed => _config.GetValue("SurveyMode:RunDemoSeed", true);
        public string BaselineBackup => _config["SurveyMode:BaselineBackup"] is { Length: > 0 } p
            ? p : "/var/opt/mssql/data/ArugaSystemDB_survey_baseline.bak";

        private TimeSpan Idle  => TimeSpan.FromMinutes(_config.GetValue("SurveyMode:IdleMinutes", 10));
        private TimeSpan Quiet => TimeSpan.FromMinutes(_config.GetValue("SurveyMode:QuietMinutes", 30));
        private TimeSpan Grace => TimeSpan.FromMinutes(_config.GetValue("SurveyMode:GraceMinutes", 2));

        // Email/SMS really go out only when this is true
        public bool MessagesAllowed => !Enabled || SendMessages;

        // ── Who is using the system ──────────────────────────────────────
        // One entry per signed-in browser (keyed by a hash of its login token,
        // so two people on the same test account count separately).
        private sealed class Session
        {
            public DateTime LastSeen;    // any request
            public DateTime LastChange;  // anything that saves (POST/PUT/PATCH/DELETE)
            public bool LoggedOut;
        }

        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly object _lock = new();
        private bool _dirty;                         // something changed since the last reset
        private DateTime _lastActivity = DateTime.MinValue;
        private readonly HashSet<string> _fromBeforeReset = new();

        public DateTime? LastResetAt { get; private set; }

        public static string KeyFor(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)), 0, 12);

        // Called for every API request that carries a login token
        public void Touch(string key, bool changesData)
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                // A tab left open from before the last reset only counts again
                // once it changes something (its polling alone doesn't hold
                // the next reset back).
                bool old = _fromBeforeReset.Contains(key);
                var s = _sessions.GetOrAdd(key, _ => new Session { LastChange = old ? DateTime.MinValue : now });
                s.LastSeen = now;
                s.LoggedOut = false;
                if (changesData)
                {
                    s.LastChange = now;
                    _dirty = true;
                    _lastActivity = now;
                    _fromBeforeReset.Remove(key);
                }
            }
        }

        // A sign-in (it writes LastLogin and an audit row)
        public void SignedIn()
        {
            lock (_lock) { _dirty = true; _lastActivity = DateTime.Now; }
        }

        public void LoggedOut(string key)
        {
            lock (_lock)
            {
                if (_sessions.TryGetValue(key, out var s)) s.LoggedOut = true;
                _lastActivity = DateTime.Now;
            }
        }

        // True when every tester has logged out or left and either something
        // changed, or it's a new day (so each morning starts with that day's
        // dates and queue, not yesterday's; also right after the API starts)
        public bool ShouldReset(out string why)
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                why = "";
                bool newDay = LastResetAt == null || LastResetAt.Value.Date < now.Date;
                if (!_dirty && !newDay) return false;
                if (now - _lastActivity < Grace) return false;

                int active = _sessions.Values.Count(s =>
                    !s.LoggedOut && now - s.LastSeen < Idle && now - s.LastChange < Quiet);
                if (active > 0) return false;

                int loggedOut = _sessions.Values.Count(s => s.LoggedOut);
                why = _dirty ? $"{loggedOut} logged out, {_sessions.Count - loggedOut} idle" : "daily refresh";
                return true;
            }
        }

        // "Send my notifications to me": a few per signed-in browser, so one
        // tester can't use up the day's texts
        private readonly ConcurrentDictionary<string, int> _testerSends = new();
        public const int TesterSendLimit = 3;

        public bool TakeTesterSend(string key) =>
            _testerSends.AddOrUpdate(key, 1, (_, n) => n + 1) <= TesterSendLimit;

        public void MarkReset()
        {
            lock (_lock)
            {
                _dirty = false;
                _lastActivity = DateTime.Now;
                _fromBeforeReset.Clear();
                foreach (var key in _sessions.Keys) _fromBeforeReset.Add(key);
                _sessions.Clear();
                LastResetAt = DateTime.Now;
            }
        }
    }

    // Checks every 30 seconds whether the test system should be reset, and
    // does it: restore the baseline, refresh the demo data to today, and
    // rebuild the vaccination schedules the same way the API does at startup.
    public class SurveyResetService : BackgroundService
    {
        private readonly SurveyMode _survey;
        private readonly IConfiguration _config;
        private readonly IServiceProvider _services;
        private readonly IHostEnvironment _env;
        private readonly ILogger<SurveyResetService> _logger;

        public SurveyResetService(SurveyMode survey, IConfiguration config, IServiceProvider services,
            IHostEnvironment env, ILogger<SurveyResetService> logger)
        {
            _survey = survey;
            _config = config;
            _services = services;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_survey.Enabled) return;
            _logger.LogWarning("Survey mode is ON: the database goes back to {Backup} after testers log out or go idle.", _survey.BaselineBackup);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (TaskCanceledException) { return; }

                if (!_survey.ShouldReset(out var why)) continue;

                try
                {
                    var started = DateTime.Now;
                    await ResetAsync(stoppingToken);
                    _survey.MarkReset();
                    _logger.LogWarning("Survey mode: test data reset ({Why}) in {Seconds:0.0}s.", why, (DateTime.Now - started).TotalSeconds);
                }
                catch (Exception ex)
                {
                    // Try again on the next round rather than hammering the database
                    _survey.MarkReset();
                    _logger.LogError(ex, "Survey mode: the reset failed; it will be tried again after the next tester.");
                }
            }
        }

        public async Task ResetAsync(CancellationToken ct)
        {
            var cs = new SqlConnectionStringBuilder(_config.GetConnectionString("DefaultConnection"));
            var database = cs.InitialCatalog;
            if (string.IsNullOrWhiteSpace(database)) throw new InvalidOperationException("The connection string has no database name.");
            var quoted = "[" + database.Replace("]", "]]") + "]";

            // 1. Put the baseline back (from master: the database itself is replaced)
            var master = new SqlConnectionStringBuilder(cs.ConnectionString) { InitialCatalog = "master", Pooling = false };
            await using (var conn = new SqlConnection(master.ConnectionString))
            {
                await conn.OpenAsync(ct);
                var sql =
                    $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    "BEGIN TRY " +
                    $"  RESTORE DATABASE {quoted} FROM DISK = @backup WITH REPLACE; " +
                    "END TRY BEGIN CATCH " +
                    $"  ALTER DATABASE {quoted} SET MULTI_USER; THROW; " +
                    "END CATCH; " +
                    $"ALTER DATABASE {quoted} SET MULTI_USER;";
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 300 };
                cmd.Parameters.AddWithValue("@backup", _survey.BaselineBackup);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // The API's pooled connections pointed at the old copy
            SqlConnection.ClearAllPools();

            // 2. Demo families: ages, due dates and today's queue from today
            if (_survey.RunDemoSeed)
            {
                var seedFile = Path.Combine(AppContext.BaseDirectory, "Database", "DemoSeed.sql");
                if (File.Exists(seedFile))
                {
                    await using var conn = new SqlConnection(cs.ConnectionString);
                    await conn.OpenAsync(ct);
                    // Run it on this API's database, whatever its "USE" line says
                    var seed = System.Text.RegularExpressions.Regex.Replace(
                        await File.ReadAllTextAsync(seedFile, ct), @"^\s*USE\s+\[?\w+\]?\s*;", "",
                        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    await using var cmd = new SqlCommand(seed, conn) { CommandTimeout = 300 };
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                else
                {
                    _logger.LogWarning("Survey mode: {File} is missing, demo dates were not refreshed.", seedFile);
                }
            }

            // 3. Same schedule checks as at startup (Program.cs)
            using var scope = _services.CreateScope();
            var timelines = scope.ServiceProvider.GetRequiredService<AndroidWebAPI.Data.IVaccinationTimelineRepository>();
            await timelines.EnsureAllTimelinesAsync();
            await timelines.LinkGivenDosesAsync();
            await timelines.MoveDosesOffClosedDaysAsync();
        }
    }
}
