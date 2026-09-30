using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClinicOperatingScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClinicOperatingScheduleController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ClinicOperatingSchedule
        [HttpGet]
        public async Task<IActionResult> GetSchedule()
        {
            var schedule = await _context.ClinicOperatingSchedules
                .Where(s => s.IsActive)
                .OrderBy(s => s.DayOfWeek)
                .ToListAsync();

            return Ok(schedule);
        }

        // GET: api/ClinicOperatingSchedule/today
        // What the parent pages show: the clinic's general hours, the
        // vaccination hours in words ("Mon, Wed, Fri · 8:00 AM – 12:00 PM"),
        // whether vaccinations run today (a holiday / special opening under
        // Operating Hours wins), and the vaccination weekdays + exceptions for
        // the Schedule page calendar.
        [HttpGet("today")]
        public async Task<IActionResult> GetToday([FromServices] IConfiguration config)
        {
            var today = DateTime.Today;
            var hoursText = await AndroidWebAPI.Services.ClinicCalendar.DescribeHoursAsync(_context);

            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e => e.IsActive && e.ExceptionDate == today);
            var weekly = await _context.ClinicOperatingSchedules
                .FirstOrDefaultAsync(s => s.IsActive && s.DayOfWeek == (int)today.DayOfWeek);

            bool open = exception?.IsOpen ?? weekly?.IsOpen ?? false;
            TimeSpan? opens = open ? exception?.OpeningTime ?? weekly?.OpeningTime : null;
            TimeSpan? closes = open ? exception?.ClosingTime ?? weekly?.ClosingTime : null;
            TimeSpan? cutoff = open ? exception?.QueueCutoffTime ?? weekly?.QueueCutoffTime : null;

            var nextOpen = await AndroidWebAPI.Services.ClinicCalendar.NextOpenDayAsync(_context, today.AddDays(1));

            var openDays = await _context.ClinicOperatingSchedules
                .Where(s => s.IsActive && s.IsOpen)
                .Select(s => s.DayOfWeek)
                .ToListAsync();
            var exceptions = await _context.ClinicScheduleExceptions
                .Where(e => e.IsActive)
                .Select(e => new { e.ExceptionDate, e.IsOpen })
                .ToListAsync();

            static string? Time(TimeSpan? t) => t == null ? null : DateTime.Today.Add(t.Value).ToString("h:mm tt");

            return Ok(new
            {
                hoursText,                                   // vaccination days + hours
                clinicHoursText = AndroidWebAPI.Services.ClinicCalendar.GeneralHours(config),
                closedToday = exception != null && !exception.IsOpen,   // holiday / typhoon, not just a non-vaccination day
                openToday = open,
                opensAt = Time(opens),
                closesAt = Time(closes),
                checkInUntil = Time(cutoff),
                checkInOpenNow = open && DateTime.Now.TimeOfDay >= opens && DateTime.Now.TimeOfDay <= cutoff,
                reason = exception?.Reason,
                nextOpenDay = nextOpen.ToString("dddd, MMMM d"),
                openDays,
                exceptions = exceptions.Select(e => new { date = e.ExceptionDate.ToString("yyyy-MM-dd"), isOpen = e.IsOpen }),
            });
        }


        // PUT: api/ClinicOperatingSchedule/{scheduleID}
[Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
[HttpPut("{scheduleID}")]
public async Task<IActionResult> UpdateSchedule(
    int scheduleID,
    UpdateClinicOperatingScheduleDto dto,
    [FromServices] IVaccinationTimelineRepository timelines)
{
    var schedule = await _context.ClinicOperatingSchedules
        .FirstOrDefaultAsync(s => s.ScheduleID == scheduleID);

    if (schedule == null)
    {
        return NotFound(new
        {
            message = "Clinic operating schedule not found."
        });
    }

    schedule.IsOpen = dto.IsOpen;
    schedule.OpeningTime = dto.OpeningTime;
    schedule.ClosingTime = dto.ClosingTime;
    schedule.QueueCutoffTime = dto.QueueCutoffTime;
    schedule.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    // A day that is now closed can't keep upcoming doses on it.
    await timelines.MoveDosesOffClosedDaysAsync();

    return Ok(schedule);
}
    }
}