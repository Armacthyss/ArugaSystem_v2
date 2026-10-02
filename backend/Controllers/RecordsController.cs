using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    public class RecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public RecordsController(AppDbContext context) { _context = context; }

        // GET /api/Records/expected-today
        [HttpGet("expected-today")]
        public async Task<IActionResult> GetExpectedToday([FromQuery] string? search)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var timelines = await _context.VaccinationTimelines
                .Include(t => t.Child)
                .Include(t => t.Vaccine)
                .Where(t => t.ScheduledDate >= today && t.ScheduledDate < tomorrow
                            && t.Status == "Pending")
                .ToListAsync();

            var result = await BuildRecordDtos(timelines, search);
            return Ok(result);
        }

        // GET /api/Records/overdue
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdue([FromQuery] string? search)
        {
            var today = DateTime.Today;

            var timelines = await _context.VaccinationTimelines
                .Include(t => t.Child)
                .Include(t => t.Vaccine)
                .Where(t => t.ScheduledDate < today
                            && (t.Status == "Pending" || t.Status == "Missed"))
                .ToListAsync();

            var result = await BuildRecordDtos(timelines, search, includeDaysOverdue: true);
            return Ok(result);
        }

        // POST /api/Records/{timelineId}/add-to-queue
        // Used by both the Expected and Overdue tabs' "Add to queue" buttons.
        [HttpPost("{timelineId}/add-to-queue")]
        public async Task<IActionResult> AddToQueue(Guid timelineId)
        {
            var timeline = await _context.VaccinationTimelines
                .FirstOrDefaultAsync(t => t.TimelineID == timelineId);
            if (timeline == null) return NotFound(new { message = "Schedule entry not found." });

            var relationship = await _context.ChildParentRelationships
                .Where(r => r.ChildID == timeline.ChildID && r.Status == "Active")
                .OrderByDescending(r => r.IsPrimaryContact)
                .FirstOrDefaultAsync();

            if (relationship == null)
                return BadRequest(new { message = "No active parent found for this child." });

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var existingQueue = await _context.Queues
                .Include(q => q.QueueChildren)
                .FirstOrDefaultAsync(q => q.ParentID == relationship.ParentID
                                          && q.QueueDate >= today && q.QueueDate < tomorrow);

            if (existingQueue != null)
            {
                if (!existingQueue.QueueChildren.Any(qc => qc.ChildID == timeline.ChildID))
                {
                    existingQueue.QueueChildren.Add(new QueueChild
                    {
                        QueueChildID = Guid.NewGuid(),
                        QueueID = existingQueue.QueueID,
                        ChildID = timeline.ChildID,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
                return Ok(new { queueID = existingQueue.QueueID, queueNumber = existingQueue.QueueNumber });
            }

            var maxNumber = await _context.Queues
                .Where(q => q.QueueDate >= today && q.QueueDate < tomorrow)
                .Select(q => (int?)q.QueueNumber)
                .MaxAsync() ?? 0;

            var queue = new Queue
            {
                QueueID = Guid.NewGuid(),
                ParentID = relationship.ParentID,
                QueueNumber = maxNumber + 1,
                QueueDate = today,
                Status = "Waiting",
                CreatedAt = DateTime.Now
            };
            queue.QueueChildren.Add(new QueueChild
            {
                QueueChildID = Guid.NewGuid(),
                QueueID = queue.QueueID,
                ChildID = timeline.ChildID,
                CreatedAt = DateTime.Now
            });

            _context.Queues.Add(queue);
            await _context.SaveChangesAsync();

            return Ok(new { queueID = queue.QueueID, queueNumber = queue.QueueNumber });
        }

        private async Task<List<object>> BuildRecordDtos(
            List<VaccinationTimeline> timelines, string? search, bool includeDaysOverdue = false)
        {
            var childIds = timelines.Select(t => t.ChildID).Distinct().ToList();

            var relationships = await _context.ChildParentRelationships
                .Include(r => r.Parent)
                .Where(r => childIds.Contains(r.ChildID) && r.Status == "Active")
                .ToListAsync();

            var today = DateTime.Today;
            var term = search?.Trim().ToLower();

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

                if (term != null && !childName.ToLower().Contains(term) && !parentName.ToLower().Contains(term))
                    continue;

                var checkedIn = await _context.QueueChildren
                    .Include(qc => qc.Queue)
                    .AnyAsync(qc => qc.ChildID == t.ChildID
                                    && qc.Queue!.QueueDate >= today && qc.Queue.QueueDate < today.AddDays(1));

                if (includeDaysOverdue)
                {
                    result.Add(new
                    {
                        recordID = t.TimelineID,
                        child = childName,
                        parent = parentName,
                        scheduledDate = t.ScheduledDate,
                        daysOverdue = (int)(today - t.ScheduledDate.Date).TotalDays
                    });
                }
                else
                {
                    result.Add(new
                    {
                        recordID = t.TimelineID,
                        child = childName,
                        parent = parentName,
                        vaccineDue = t.Vaccine?.VaccineName,
                        scheduledTime = t.ScheduledDate.TimeOfDay == TimeSpan.Zero
                            ? null
                            : t.ScheduledDate.ToString("h:mm tt"),
                        checkedIn
                    });
                }
            }
            return result;
        }
    }
}