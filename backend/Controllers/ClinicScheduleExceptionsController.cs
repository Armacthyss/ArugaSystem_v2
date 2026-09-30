using AndroidWebAPI.Data;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClinicScheduleExceptionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClinicScheduleExceptionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ClinicScheduleExceptions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClinicScheduleException>>> GetExceptions()
        {
            var exceptions = await _context.ClinicScheduleExceptions
                .Where(e => e.IsActive)
                .OrderBy(e => e.ExceptionDate)
                .ToListAsync();

            return Ok(exceptions);
        }

        // GET: api/ClinicScheduleExceptions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ClinicScheduleException>> GetException(int id)
        {
            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionID == id);

            if (exception == null)
                return NotFound(new { message = "Schedule exception not found." });

            return Ok(exception);
        }

        // GET: api/ClinicScheduleExceptions/date/2026-08-26
        [HttpGet("date/{date}")]
        public async Task<ActionResult<ClinicScheduleException>> GetExceptionByDate(DateTime date)
        {
            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e =>
                    e.ExceptionDate.Date == date.Date &&
                    e.IsActive);

            if (exception == null)
                return NotFound(new { message = "No active exception exists for this date." });

            return Ok(exception);
        }

        // POST: api/ClinicScheduleExceptions
        // One date, or every date up to EndDate (e.g. a typhoon closing the
        // health center for several days). On a closed date, doses due that
        // day move to the next vaccination day and parents are notified
        // (see Services/ClosureNotices.cs).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPost]
        public async Task<IActionResult> CreateException(
            ClinicScheduleExceptionDto dto,
            [FromServices] IVaccinationTimelineRepository timelines,
            [FromServices] ParentNotifier notifier,
            [FromServices] AuditService audit)
        {
            var start = dto.ExceptionDate.Date;
            var end = (dto.EndDate ?? dto.ExceptionDate).Date;

            if (end < start)
                return BadRequest(new { message = "The last date can't be before the first date." });
            if ((end - start).Days > 30)
                return BadRequest(new { message = "Add at most 31 days at a time." });

            var dates = Enumerable.Range(0, (end - start).Days + 1).Select(i => start.AddDays(i)).ToList();

            var taken = await _context.ClinicScheduleExceptions
                .Where(e => e.IsActive && e.ExceptionDate >= start && e.ExceptionDate <= end)
                .Select(e => e.ExceptionDate)
                .ToListAsync();

            if (taken.Any())
            {
                return Conflict(new
                {
                    message = dates.Count == 1
                        ? "An active schedule exception already exists for this date."
                        : $"An active schedule exception already exists for {string.Join(", ", taken.OrderBy(d => d).Select(d => d.ToString("MMM d")))}. Edit that date instead."
                });
            }

            // Doses due on the closed dates, before they are moved
            var dueThen = dto.IsOpen ? new List<Guid>() : await ClosureNotices.DosesDueOnAsync(_context, dates);

            var created = dates.Select(date => new ClinicScheduleException
            {
                ExceptionDate = date,
                IsOpen = dto.IsOpen,
                OpeningTime = dto.OpeningTime,
                ClosingTime = dto.ClosingTime,
                QueueCutoffTime = dto.QueueCutoffTime,
                Reason = dto.Reason,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.ClinicScheduleExceptions.AddRange(created);
            await _context.SaveChangesAsync();

            // Doses due on a newly closed day move to the next open day.
            await timelines.MoveDosesOffClosedDaysAsync();

            var sent = dto.IsOpen
                ? new ParentNotifier.Delivery()
                : await ClosureNotices.SendAsync(_context, notifier, dates, dto.Reason, dueThen);

            await audit.LogAsync("Operating Hours", "Create",
                $"Schedule exception – {start:MMM d, yyyy}{(dates.Count > 1 ? $" to {end:MMM d, yyyy}" : "")}",
                dto.IsOpen
                    ? $"Open for vaccinations{(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" ({dto.Reason})")}."
                    : $"Closed{(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" ({dto.Reason})")}. {dueThen.Count} dose(s) moved; {sent.InApp} parent(s) notified.");

            return CreatedAtAction(
                nameof(GetException),
                new { id = created[0].ExceptionID },
                new
                {
                    exceptions = created,
                    dosesMoved = dueThen.Count,
                    parentsNotified = sent.InApp,
                    emails = sent.Emails,
                    texts = sent.Texts,
                });
        }

        // PUT: api/ClinicScheduleExceptions/5
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateException(
            int id,
            ClinicScheduleExceptionDto dto,
            [FromServices] IVaccinationTimelineRepository timelines,
            [FromServices] ParentNotifier notifier)
        {
            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionID == id);

            if (exception == null)
                return NotFound(new { message = "Schedule exception not found." });

            var newDate = dto.ExceptionDate.Date;
            if (newDate != exception.ExceptionDate.Date &&
                await _context.ClinicScheduleExceptions.AnyAsync(e => e.IsActive && e.ExceptionID != id && e.ExceptionDate == newDate))
                return Conflict(new { message = "An active schedule exception already exists for this date." });

            // Parents are told when a date becomes closed (not when only the
            // reason or times change).
            bool newlyClosed = !dto.IsOpen && (exception.IsOpen || newDate != exception.ExceptionDate.Date);
            var dueThen = newlyClosed ? await ClosureNotices.DosesDueOnAsync(_context, new[] { newDate }) : new List<Guid>();

            exception.ExceptionDate = newDate;
            exception.IsOpen = dto.IsOpen;
            exception.OpeningTime = dto.OpeningTime;
            exception.ClosingTime = dto.ClosingTime;
            exception.QueueCutoffTime = dto.QueueCutoffTime;
            exception.Reason = dto.Reason;
            exception.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await timelines.MoveDosesOffClosedDaysAsync();

            var sent = newlyClosed
                ? await ClosureNotices.SendAsync(_context, notifier, new[] { newDate }, dto.Reason, dueThen)
                : new ParentNotifier.Delivery();

            return Ok(new
            {
                exception,
                dosesMoved = dueThen.Count,
                parentsNotified = sent.InApp,
                emails = sent.Emails,
                texts = sent.Texts,
            });
        }

        // DELETE: api/ClinicScheduleExceptions/5
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteException(
            int id,
            [FromServices] IVaccinationTimelineRepository timelines)
        {
            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionID == id);

            if (exception == null)
                return NotFound(new { message = "Schedule exception not found." });

            exception.IsActive = false;
            exception.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Removing a special opening day closes it again.
            await timelines.MoveDosesOffClosedDaysAsync();

            return Ok(new
            {
                message = "Schedule exception deactivated successfully."
            });
        }
    }
}