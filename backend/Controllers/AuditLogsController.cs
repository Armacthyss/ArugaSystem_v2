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
        // Newest first. Both dates are optional and inclusive; with neither,
        // the last 30 days are returned so the admin page stays fast as the
        // table grows.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpGet]
        public async Task<IActionResult> GetLogs([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var start = (from ?? DateTime.Today.AddDays(-30)).Date;
            var end = (to ?? DateTime.Today).Date.AddDays(1);

            var logs = await _context.AuditLogs
                .Where(l => l.ActionDate >= start && l.ActionDate < end)
                .OrderByDescending(l => l.ActionDate)
                .ThenByDescending(l => l.AuditID)
                .Take(2000)
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

            var result = logs.Select(l =>
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

                return new
                {
                    id = l.AuditID,
                    timestamp = l.ActionDate,
                    userID = l.UserID,
                    user = string.IsNullOrWhiteSpace(name) ? "System" : name,
                    role = FriendlyRole(role) ?? "System",
                    module = payload?.Module ?? "General",
                    action = payload?.Action ?? "Activity",
                    affectedRecord = payload?.AffectedRecord ?? "—",
                    description = payload?.Description ?? l.ActionPerformed,
                    status = payload?.Status ?? "Success",
                    ipAddress = payload?.IpAddress ?? "—",
                    device = payload?.Device ?? "—",
                    oldValue = payload?.OldValue ?? "—",
                    newValue = payload?.NewValue ?? "—",
                };
            });

            return Ok(result);
        }

        private static string RoleFor(string? position) => Roles.ForPosition(position);

        // JWT role values -> labels the admin page shows.
        private static string? FriendlyRole(string? role) => role switch
        {
            "SystemAdmin" => "Admin / Doctor",
            "Healthcare" => "Staff / Nurse",   // entries from before the 3-level change
            "Staff" => "Staff / Nurse",
            _ => role,
        };
    }
}
