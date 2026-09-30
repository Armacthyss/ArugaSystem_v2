using AndroidWebAPI.Data;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    // GET /api/SystemStatus
    // The Super Admin dashboard: how many accounts each level has, today's
    // sign-ins, locked accounts, and whether Email / SMS / CAPTCHA are on.
    // Counts only; no patient records.
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.SuperAdmin)]
    public class SystemStatusController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SystemStatusController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromServices] MessageSender sender, [FromServices] Captcha captcha)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            var accounts = await _context.Accounts
                .Select(a => new { a.AccountType, a.ReferenceID, a.Status, a.LockedUntil })
                .ToListAsync();
            var positions = await _context.Users.ToDictionaryAsync(u => u.UserID, u => u.Position);

            string LevelOf(string type, Guid reference)
            {
                if (type == "Parent") return "Parent";
                positions.TryGetValue(reference, out var position);
                return Roles.ForPosition(position) switch
                {
                    Roles.SuperAdmin => "Super Admin",
                    Roles.Admin => "Admin / Doctor",
                    _ => "Staff / Nurse",
                };
            }

            var levels = new[] { "Super Admin", "Admin / Doctor", "Staff / Nurse", "Parent" }
                .Select(level =>
                {
                    var list = accounts.Where(a => LevelOf(a.AccountType, a.ReferenceID) == level).ToList();
                    return new { level, total = list.Count, active = list.Count(a => a.Status) };
                });

            // Today's sign-ins, from the audit trail (JSON payloads)
            var loginsToday = await _context.AuditLogs
                .Where(l => l.ActionDate >= today && l.ActionPerformed.Contains("\"Module\":\"Authentication\"")
                         && l.ActionPerformed.Contains("\"Action\":\"Login\""))
                .Select(l => l.ActionPerformed)
                .ToListAsync();

            return Ok(new
            {
                generatedAt = now,
                accounts = levels,
                lockedAccounts = accounts.Count(a => a.LockedUntil > now),
                inactiveAccounts = accounts.Count(a => !a.Status),
                signInsToday = loginsToday.Count(p => p.Contains("\"Status\":\"Success\"")),
                failedSignInsToday = loginsToday.Count(p => !p.Contains("\"Status\":\"Success\"")),
                messaging = new
                {
                    email = sender.EmailEnabled,
                    sms = sender.SmsEnabled,
                    smsProvider = sender.SmsProvider,
                    smsSentToday = MessageSender.SmsSentToday,
                    smsDailyLimit = sender.SmsDailyLimit,
                },
                captcha = captcha.Enabled,
            });
        }
    }
}
