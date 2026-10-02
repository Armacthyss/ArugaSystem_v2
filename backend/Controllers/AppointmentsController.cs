using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AppointmentsController(AppDbContext context) { _context = context; }

        // GET /api/Appointments/today?search=
        // Manual-search version of the Expected tab: same VaccinationTimeline
        // data, scoped to today, excluding children already checked in —
        // used by the "Add to Today's Queue" modal for patients without a
        // phone to scan the QR code themselves.
        [HttpGet("today")]
        public async Task<IActionResult> GetToday([FromQuery] string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return Ok(new List<object>());

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var term = search.Trim().ToLower();

            var timelines = await _context.VaccinationTimelines
                .Include(t => t.Child)
                .Where(t => t.ScheduledDate >= today && t.ScheduledDate < tomorrow
                            && t.Status == "Pending")
                .ToListAsync();

            var childIds = timelines.Select(t => t.ChildID).Distinct().ToList();

            var relationships = await _context.ChildParentRelationships
                .Include(r => r.Parent)
                .Where(r => childIds.Contains(r.ChildID) && r.Status == "Active")
                .ToListAsync();

            var result = new List<object>();
            foreach (var t in timelines)
            {
                var rel = relationships
                    .Where(r => r.ChildID == t.ChildID)
                    .OrderByDescending(r => r.IsPrimaryContact)
                    .FirstOrDefault();

                var childName = $"{t.Child?.FirstName} {t.Child?.LastName}".Trim();
                var parentName = rel?.Parent != null
                    ? $"{rel.Parent.FirstName} {rel.Parent.LastName}".Trim()
                    : "—";

                if (!childName.ToLower().Contains(term) && !parentName.ToLower().Contains(term))
                    continue;

                var checkedIn = await _context.QueueChildren
                    .Include(qc => qc.Queue)
                    .AnyAsync(qc => qc.ChildID == t.ChildID
                                    && qc.Queue!.QueueDate >= today && qc.Queue.QueueDate < tomorrow);
                if (checkedIn) continue;

                result.Add(new
                {
                    appointmentID = t.TimelineID,
                    child = childName,
                    parent = parentName
                });
            }

            return Ok(result);
        }
    }
}