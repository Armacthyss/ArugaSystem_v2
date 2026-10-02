using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StaffController(AppDbContext context) { _context = context; }

        // GET /api/Staff/status
        // "active"/"occupied"/"inactive" for the Queue page's staff strip.
        // No real login-session tracking exists yet, so:
        //   - inactive = Account.Status is disabled
        //   - occupied = assigned to an InProgress queue visit today
        //   - active   = enabled account, not currently occupied
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var personnelAccounts = await (
                from p in _context.Personnel
                join a in _context.Accounts on p.PersonnelID equals a.ReferenceID
                where a.AccountType == "Personnel"
                select new { p, a }
            ).ToListAsync();

            var occupiedStaffIds = await _context.Queues
                .Where(q => q.QueueDate >= today && q.QueueDate < tomorrow
                            && q.Status == "InProgress"
                            && q.AssignedStaffID != null)
                .Select(q => q.AssignedStaffID!.Value)
                .ToListAsync();

            var result = personnelAccounts.Select(x => new
            {
                staffID = x.p.PersonnelID,
                name = $"{x.p.FirstName} {x.p.LastName}".Trim(),
                role = x.p.Role,
                status = !x.a.Status
                    ? "inactive"
                    : occupiedStaffIds.Contains(x.p.PersonnelID) ? "occupied" : "active"
            });

            return Ok(result);
        }
    }
}