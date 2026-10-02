using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    public class OperatingHoursController : ControllerBase
    {
        private readonly AppDbContext _context;
        public OperatingHoursController(AppDbContext context) { _context = context; }

        // GET /api/OperatingHours/today
        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            var today = DateTime.Today;

            // Date-specific override (holiday, special closure/hours) wins
            // over the recurring weekly schedule.
            var exception = await _context.ClinicScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionDate.Date == today && e.IsActive);

            if (exception != null)
            {
                if (!exception.IsOpen)
                {
                    return Ok(new
                    {
                        open = false,
                        openTime = (string?)null,
                        closeTime = (string?)null,
                        reason = string.IsNullOrWhiteSpace(exception.Reason)
                            ? "The clinic is closed today."
                            : exception.Reason
                    });
                }

                return Ok(new
                {
                    open = true,
                    openTime = FormatTime(exception.OpeningTime),
                    closeTime = FormatTime(exception.ClosingTime),
                    reason = (string?)null
                });
            }

            // Fall back to the recurring weekly schedule.
            // .NET's DayOfWeek enum is already 0=Sunday..6=Saturday, matching
            // ClinicOperatingSchedule.DayOfWeek's convention.
            var dayOfWeek = (int)today.DayOfWeek;
            var schedule = await _context.ClinicOperatingSchedules
                .FirstOrDefaultAsync(s => s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null || !schedule.IsOpen)
            {
                return Ok(new
                {
                    open = false,
                    openTime = (string?)null,
                    closeTime = (string?)null,
                    reason = "No operating hours are set for today."
                });
            }

            return Ok(new
            {
                open = true,
                openTime = FormatTime(schedule.OpeningTime),
                closeTime = FormatTime(schedule.ClosingTime),
                reason = (string?)null
            });
        }

        private static string FormatTime(TimeSpan t) =>
            DateTime.Today.Add(t).ToString("h:mm tt");
    }
}