using System.Security.Claims;
using System.Text.Json;
using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Writes one row to dbo.AuditLogs per important action (logins,
    // vaccinations, account changes, inventory changes...).
    //
    // Who did it is taken from the JWT on the current request when the
    // caller doesn't pass it explicitly — the frontend attaches the token
    // to every API call (see main.js), so this works without having to
    // thread the user through every controller.
    //
    // Logging must never break the action being logged, so every failure
    // here is swallowed and only written to the server log.
    public class AuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, IHttpContextAccessor http, ILogger<AuditService> logger)
        {
            _context = context;
            _http = http;
            _logger = logger;
        }

        public async Task LogAsync(
            string module,
            string action,
            string? affectedRecord = null,
            string? description = null,
            string status = "Success",
            Guid? userId = null,
            string? userName = null,
            string? role = null,
            string? oldValue = null,
            string? newValue = null)
        {
            AuditLog? entry = null;
            try
            {
                var ctx = _http.HttpContext;
                var principal = ctx?.User;

                if (userId == null &&
                    Guid.TryParse(principal?.FindFirst("ReferenceID")?.Value, out var claimUser))
                {
                    userId = claimUser;
                }

                role ??= principal?.FindFirst(ClaimTypes.Role)?.Value;

                // AuditLogs.UserID has a foreign key to dbo.Users, so only
                // personnel can be linked directly. Parents (and anything
                // else) are recorded by name inside the payload instead.
                if (userId.HasValue && !await _context.Users.AnyAsync(u => u.UserID == userId.Value))
                {
                    var parent = await _context.Parents.FirstOrDefaultAsync(p => p.ParentID == userId.Value);
                    if (parent != null)
                    {
                        userName ??= $"{parent.FirstName} {parent.LastName}".Trim();
                        role ??= "Parent";
                    }
                    userId = null;
                }

                var payload = new AuditPayload
                {
                    Module = module,
                    Action = action,
                    AffectedRecord = affectedRecord,
                    Description = description,
                    Status = status,
                    UserName = userName,
                    Role = role,
                    IpAddress = ClientIp(ctx),
                    Device = DescribeDevice(ctx?.Request.Headers.UserAgent.ToString()),
                    OldValue = oldValue,
                    NewValue = newValue,
                };

                entry = new AuditLog
                {
                    UserID = userId,
                    ActionPerformed = JsonSerializer.Serialize(payload),
                    ActionDate = DateTime.Now,
                };
                _context.AuditLogs.Add(entry);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write audit log for {Module}/{Action}", module, action);

                // Don't leave the failed row in the (request-scoped) change
                // tracker, or the caller's next SaveChanges would retry it.
                if (entry != null)
                    _context.Entry(entry).State = EntityState.Detached;
            }
        }

        // Phones reach the API through the website's dev server on this
        // computer (frontend/vite.config.js), so the connection itself comes
        // from this computer; the phone's own address is in X-Forwarded-For.
        // That header is only trusted when the request came from this computer.
        private static string? ClientIp(HttpContext? ctx)
        {
            var remote = ctx?.Connection.RemoteIpAddress;
            if (remote != null && System.Net.IPAddress.IsLoopback(remote))
            {
                var forwarded = ctx!.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim();
                if (System.Net.IPAddress.TryParse(forwarded, out var phone))
                    return Plain(phone);
            }
            return remote == null ? null : Plain(remote);

            // "::ffff:192.168.1.3" -> "192.168.1.3"
            static string Plain(System.Net.IPAddress ip) =>
                (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
        }

        // "Chrome on Windows" style label — enough for an admin reading the
        // log without storing the whole user-agent string.
        private static string? DescribeDevice(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return null;

            string browser =
                userAgent.Contains("Edg/") ? "Edge" :
                userAgent.Contains("OPR/") ? "Opera" :
                userAgent.Contains("Chrome/") ? "Chrome" :
                userAgent.Contains("Firefox/") ? "Firefox" :
                userAgent.Contains("Safari/") ? "Safari" : "Browser";

            string os =
                userAgent.Contains("Windows") ? "Windows" :
                userAgent.Contains("Android") ? "Android" :
                userAgent.Contains("iPhone") || userAgent.Contains("iPad") ? "iOS" :
                userAgent.Contains("Mac OS") ? "macOS" :
                userAgent.Contains("Linux") ? "Linux" : "Unknown OS";

            return $"{browser} on {os}";
        }
    }

    public class AuditPayload
    {
        public string Module { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? AffectedRecord { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Success";
        public string? UserName { get; set; }
        public string? Role { get; set; }
        public string? IpAddress { get; set; }
        public string? Device { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}
