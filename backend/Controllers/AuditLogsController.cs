using System.Text.Json;
using AndroidWebAPI.Data;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/AuditLogs?from=2026-09-01&to=2026-09-30
        // The full audit trail, for the Super Admin only: the people whose
        // actions are logged don't review their own logs. Newest first. Both
        // dates are optional and inclusive; with neither, the last 30 days are
        // returned so the page stays fast as the table grows.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.SuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> GetLogs([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var start = (from ?? DateTime.Today.AddDays(-30)).Date;
            var end = (to ?? DateTime.Today).Date.AddDays(1);
            return Ok(await BuildAsync(start, end, 2000));
        }

        // GET /api/AuditLogs/activity
        // "Recent Clinic Activities" on the Doctor's and Nurse's dashboards:
        // the last week of clinic work only (queue, vaccinations, patients,
        // inventory...). Sign-ins, account changes and anything a Super Admin
        // did stay in the full audit trail.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.ClinicTeam)]
        [HttpGet("activity")]
        public async Task<IActionResult> GetClinicActivity()
        {
            var hidden = new[] { "Authentication", "User Management" };
            var list = (await BuildAsync(DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1), 400))
                .Where(e => !hidden.Contains(e.module) && e.role != "Super Admin")
                .Take(50);
            return Ok(list);
        }

        // One row of the log as the pages show it (camelCase like the rest of the API)
        public record LogEntry(
            long id, DateTime? timestamp, Guid? userID, string user, string role, string module, string action,
            string affectedRecord, string? description, string status, string ipAddress, string device,
            string oldValue, string newValue);

        private async Task<List<LogEntry>> BuildAsync(DateTime start, DateTime end, int max)
        {
            var logs = await _context.AuditLogs
                .Where(l => l.ActionDate >= start && l.ActionDate < end)
                .OrderByDescending(l => l.ActionDate)
                .ThenByDescending(l => l.AuditID)
                .Take(max)
                .ToListAsync();

            // Resolve display names for whoever performed each action. The
            // UserID column can point at a personnel Users row, a Parents
            // row, or (for the system admin) an Accounts.ReferenceID.
            var ids = logs.Where(l => l.UserID.HasValue).Select(l => l.UserID!.Value).Distinct().ToList();

            var users = await _context.Users
                .Where(u => ids.Contains(u.UserID))
                .ToDictionaryAsync(u => u.UserID);

            var parents = await _context.Parents
                .Where(p => ids.Contains(p.ParentID))
                .ToDictionaryAsync(p => p.ParentID);

            return logs.Select(l =>
            {
                AuditPayload? payload = null;
                if (!string.IsNullOrWhiteSpace(l.ActionPerformed) && l.ActionPerformed.TrimStart().StartsWith("{"))
                {
                    try { payload = JsonSerializer.Deserialize<AuditPayload>(l.ActionPerformed); }
                    catch { payload = null; }
                }

                string? name = payload?.UserName;
                string? role = payload?.Role;

                if (l.UserID.HasValue && users.TryGetValue(l.UserID.Value, out var user))
                {
                    name ??= $"{user.FirstName} {user.LastName}".Trim();
                    role ??= RoleFor(user.Position);
                }
                else if (l.UserID.HasValue && parents.TryGetValue(l.UserID.Value, out var parent))
                {
                    name ??= $"{parent.FirstName} {parent.LastName}".Trim();
                    role ??= "Parent";
                }

                return new LogEntry(
                    l.AuditID,
                    l.ActionDate,
                    l.UserID,
                    string.IsNullOrWhiteSpace(name) ? "System" : name,
                    FriendlyRole(role) ?? "System",
                    payload?.Module ?? "General",
                    payload?.Action ?? "Activity",
                    payload?.AffectedRecord ?? "—",
                    payload?.Description ?? l.ActionPerformed,
                    payload?.Status ?? "Success",
                    payload?.IpAddress ?? "—",
                    payload?.Device ?? "—",
                    payload?.OldValue ?? "—",
                    payload?.NewValue ?? "—");
            }).ToList();
        }

        private static string RoleFor(string? position) => Roles.ForPosition(position);

        // JWT role values -> labels the pages show.
        private static string? FriendlyRole(string? role) => role switch
        {
            Roles.SuperAdmin => "Super Admin",
            "SystemAdmin" => "Admin / Doctor",
            "Healthcare" => "Staff / Nurse",   // entries from before the 3-level change
            "Staff" => "Staff / Nurse",
            _ => role,
        };
    }
}
