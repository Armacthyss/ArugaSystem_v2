using AndroidWebAPI.Data;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using AndroidWebAPI.DTOs;

    namespace AndroidWebAPI.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class VaccinationTimelineController : ControllerBase
        {
            private readonly IVaccinationTimelineRepository _repository;

            public VaccinationTimelineController(IVaccinationTimelineRepository repository)
            {
                _repository = repository;
            }

            // GET: api/VaccinationTimeline
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpGet]
            public async Task<IActionResult> GetAll()
            {
                var timelines = await _repository.GetAllAsync();
                return Ok(timelines);
            }

            // GET: api/VaccinationTimeline/{timelineId}
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpGet("{timelineId}")]
            public async Task<IActionResult> GetById(Guid timelineId)
            {
                var timeline = await _repository.GetByIdAsync(timelineId);

                if (timeline == null)
                    return NotFound();

                return Ok(timeline);
            }

            // GET: api/VaccinationTimeline/child/{childId}
            [HttpGet("child/{childId}")]
public async Task<IActionResult> GetByChild(Guid childId, [FromServices] AppDbContext context)
{
    if (!await AndroidWebAPI.Services.AccessGuard.CanSeeChildAsync(User, context, childId)) return Forbid();
    await _repository.EnsureTimelineAsync(childId);
    var timelines = await _repository.GetByChildAsync(childId);

    var result = timelines.Select(t => new
    {
        timelineID = t.TimelineID,
        vaccineID = t.VaccineID,
        vaccineName = t.Vaccine?.VaccineName ?? "Unknown",
        doseNumber = t.DoseNumber,
        expectedDate = t.ExpectedDate,
        scheduledDate = t.ScheduledDate,
        status = t.Status,
        vaccinationRecordID = t.VaccinationRecordID
    });

    return Ok(result);
}

            // POST: api/VaccinationTimeline/generate/{childId}
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpPost("generate/{childId}")]
            public async Task<IActionResult> Generate(Guid childId)
            {
                await _repository.GenerateTimelineAsync(childId);

                return Ok(new
                {
                    message = "Vaccination timeline generated successfully."
                });
            }

            // PUT: api/VaccinationTimeline/complete/{timelineId}
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpPut("complete/{timelineId}")]
            public async Task<IActionResult> MarkCompleted(Guid timelineId)
            {
                await _repository.MarkCompletedAsync(timelineId);

                return Ok(new
                {
                    message = "Vaccination marked as completed."
                });
            }

            // POST: api/VaccinationTimeline/regenerate/{childId}
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpPost("regenerate/{childId}")]
            public async Task<IActionResult> Regenerate(Guid childId)
            {
                await _repository.RegenerateTimelineAsync(childId);

                return Ok(new
                {
                    message = "Vaccination timeline regenerated."
                });
            }

            // GET: api/VaccinationTimeline/due-today
            // Shape expected by the Doctor Home dashboard's "Pending
            // Vaccinations Today" panel — timelineId identifies the
            // VaccinationTimeline row (there is no VaccinationRecords row
            // yet for a Pending dose, so there is deliberately no recordId
            // here). This replaces GET /api/VaccinationRecords/pending-today,
            // which read from VaccinationRecords — a table nothing in the
            // app ever populates with Status "Scheduled" — and so always
            // returned []. VaccinationTimeline (Status "Pending") is the
            // actual source of truth for "what's due."
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpGet("due-today")]
            public async Task<IActionResult> GetDueToday()
            {
                var timelines = await _repository.GetDueTodayAsync();

                var result = timelines.Select(t => new
                {
                    timelineId = t.TimelineID,
                    childId = t.ChildID,
                    childName = t.Child != null ? $"{t.Child.FirstName} {t.Child.LastName}".Trim() : "Unknown",
                    parentName = GetPrimaryParentName(t.Child),
                    vaccineId = t.VaccineID,
                    vaccineName = t.Vaccine != null ? t.Vaccine.VaccineName : $"Vaccine {t.VaccineID}",
                    doseNumber = t.DoseNumber,
                    scheduledDate = t.ScheduledDate,
                });

                return Ok(result);
            }

            // Mirrors VaccinationRecordsController's helper of the same name.
            private static string? GetPrimaryParentName(AndroidWebAPI.Models.Child? child)
            {
                var primaryParent = child?.ParentRelationships?
                    .FirstOrDefault(pr => pr.IsPrimaryContact && pr.Status == "Active")?
                    .Parent;

                return primaryParent != null
                    ? $"{primaryParent.FirstName} {primaryParent.LastName}".Trim()
                    : null;
            }

            // GET: api/VaccinationTimeline/schedule?from=2026-09-01&to=2026-09-30&includeOverdue=true
            // Flattened schedule for the Admission Staff "Vaccine Schedule"
            // page and the dashboards: one row per timeline dose in the date
            // range, with child + primary parent details already joined.
            // includeOverdue also pulls in every not-yet-given dose dated
            // before `from`, so late children don't drop off the list.
            // `displayStatus` is what the UI shows:
            //   Completed | Due Today | Upcoming | Overdue
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpGet("schedule")]
            public async Task<IActionResult> GetSchedule(
                [FromServices] AppDbContext context,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] bool includeOverdue = true)
            {
                var today = DateTime.Today;
                var start = (from ?? today).Date;
                var end = (to ?? today.AddDays(30)).Date;

                var rows = await context.VaccinationTimelines
                    .Include(t => t.Child)
                        .ThenInclude(c => c.ParentRelationships)
                        .ThenInclude(r => r.Parent)
                    .Include(t => t.Vaccine)
                    .Where(t =>
                        (t.ScheduledDate >= start && t.ScheduledDate <= end) ||
                        (includeOverdue && t.ScheduledDate < start &&
                         (t.Status == "Pending" || t.Status == "Missed")))
                    .OrderBy(t => t.ScheduledDate)
                    .ThenBy(t => t.Child.LastName)
                    .ToListAsync();

                var result = rows.Select(t =>
                {
                    var link = t.Child?.ParentRelationships?
                        .FirstOrDefault(r => r.IsPrimaryContact && r.Status == "Active")
                        ?? t.Child?.ParentRelationships?.FirstOrDefault(r => r.Status == "Active");

                    string displayStatus =
                        t.Status == "Completed" ? "Completed" :
                        t.ScheduledDate.Date < today ? "Overdue" :
                        t.ScheduledDate.Date == today ? "Due Today" : "Upcoming";

                    return new
                    {
                        timelineID = t.TimelineID,
                        childID = t.ChildID,
                        childName = t.Child != null ? $"{t.Child.FirstName} {t.Child.LastName}".Trim() : "Unknown",
                        birthDate = t.Child?.BirthDate,
                        sex = t.Child?.Sex,
                        parentID = link?.ParentID,
                        parentName = link?.Parent != null ? $"{link.Parent.FirstName} {link.Parent.LastName}".Trim() : null,
                        parentContact = link?.Parent?.ContactNo,
                        vaccineID = t.VaccineID,
                        vaccineName = t.Vaccine?.VaccineName ?? $"Vaccine {t.VaccineID}",
                        abbreviation = t.Vaccine?.Abbreviation,
                        doseNumber = t.DoseNumber,
                        expectedDate = t.ExpectedDate,
                        scheduledDate = t.ScheduledDate,
                        completedDate = t.CompletedDate,
                        status = t.Status,
                        displayStatus,
                    };
                });

                return Ok(result);
            }

            // GET: api/VaccinationTimeline/upcoming/{days}
            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
            [HttpGet("upcoming/{days}")]
            public async Task<IActionResult> GetUpcoming(int days)
            {
                var timelines = await _repository.GetUpcomingAsync(days);
                return Ok(timelines);
            }
    [HttpGet("summary/{childId}")]
    public async Task<IActionResult> GetSummary(Guid childId, [FromServices] AppDbContext context)
    {
        if (!await AndroidWebAPI.Services.AccessGuard.CanSeeChildAsync(User, context, childId)) return Forbid();
        var summary = await _repository.GetTimelineSummaryAsync(childId);
        return Ok(summary);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    [HttpPut("update-missed")]
    public async Task<IActionResult> UpdateMissed()
    {
        await _repository.UpdateMissedVaccinationsAsync();

        return Ok(new
        {
            message = "Missed vaccinations updated successfully."
        });
    }
        }
    }