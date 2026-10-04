using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    // Survey mode only (see Service/SurveyMode.cs).
    //
    // POST /api/survey/notify-me  { childId, email, phone }
    // A parent tester types in their OWN email and/or phone number and gets
    // the reminder for their child's next dose by email, SMS and in the app,
    // so they can see for themselves that notifications arrive. The email and
    // number are used once and never saved; the account's own (made-up)
    // contact details are never messaged.
    [ApiController]
    [Route("api/survey")]
    [Authorize(Roles = Roles.Parent)]
    public class SurveyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SurveyMode _survey;
        private readonly MessageSender _sender;

        public SurveyController(AppDbContext context, SurveyMode survey, MessageSender sender)
        {
            _context = context;
            _survey = survey;
            _sender = sender;
        }

        public class NotifyMeDto
        {
            public Guid ChildId { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
        }

        [HttpPost("notify-me")]
        public async Task<IActionResult> NotifyMe([FromBody] NotifyMeDto dto)
        {
            if (!_survey.Enabled) return NotFound();

            var email = dto.Email?.Trim();
            var phone = dto.Phone?.Trim();
            if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(phone))
                return BadRequest(new { message = "Please type your email address or phone number." });
            if (!string.IsNullOrEmpty(email) && !IsEmail(email))
                return BadRequest(new { message = "Please check the email address." });
            if (!string.IsNullOrEmpty(phone) && MessageSender.NormalizePhNumber(phone) == null)
                return BadRequest(new { message = "Please enter a Philippine mobile number, e.g. 0917 123 4567." });

            if (!await AccessGuard.CanSeeChildAsync(User, _context, dto.ChildId)) return Forbid();
            var parentId = AccessGuard.CallerId(User);
            var parent = await _context.Parents.FirstOrDefaultAsync(p => p.ParentID == parentId);
            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildID == dto.ChildId);
            if (parent == null || child == null) return NotFound(new { message = "Child not found." });

            var token = Request.Headers.Authorization.ToString();
            if (!_survey.TakeTesterSend(SurveyMode.KeyFor(token.Length > 7 ? token[7..].Trim() : token)))
                return BadRequest(new { message = $"You've already sent {SurveyMode.TesterSendLimit} test notifications. Thank you for testing!" });

            // The next dose still to be given (the earliest open dose of each vaccine)
            var open = await _context.VaccinationTimelines
                .Include(t => t.Vaccine)
                .Where(t => t.ChildID == child.ChildID && (t.Status == "Pending" || t.Status == "Missed"))
                .ToListAsync();
            var next = open
                .GroupBy(t => t.VaccineID).Select(g => g.OrderBy(t => t.DoseNumber).First())
                .OrderBy(t => t.ScheduledDate).ThenBy(t => t.VaccineID)
                .FirstOrDefault();

            var hours = await ClinicCalendar.DescribeHoursAsync(_context);
            string type, title, message, sms;
            if (next != null)
            {
                (type, title, message, sms) = NotificationGeneratorService.SampleReminder(
                    next.Vaccine?.VaccineName ?? $"Vaccine {next.VaccineID}", next.Vaccine?.Abbreviation,
                    next.DoseNumber, child.FirstName, child.LastName, next.ScheduledDate, DateTime.Today, hours);
            }
            else
            {
                type = "Reminder";
                title = $"Vaccinations complete — {child.FirstName} {child.LastName}";
                message = $"{child.FirstName} {child.LastName} has received every scheduled vaccine. Thank you for keeping {child.FirstName} protected!";
                sms = $"Leveriza Health Center: {child.FirstName} has received every scheduled vaccine. Thank you!";
            }

            // In the app (the bell), like a real reminder
            var inApp = new Notification
            {
                NotificationID = Guid.NewGuid(),
                ParentID = parent.ParentID,
                ChildID = child.ChildID,
                VaccineID = next?.VaccineID,
                DoseNumber = next?.DoseNumber,
                Type = type,
                Title = title,
                Message = message,
                ScheduledDate = next?.ScheduledDate,
                IsRead = false,
                CreatedAt = DateTime.Now,
            };
            ParentNotifier.FitToColumns(inApp);
            _context.Notifications.Add(inApp);
            await _context.SaveChangesAsync();

            // Email and SMS to what the tester typed in
            const string note = "This is a test message from the Aruga survey. Your email address and number were not saved.";
            string? emailResult = null, smsResult = null;
            if (!string.IsNullOrEmpty(email))
                emailResult = await _sender.TestEmailAsync(email, title, $"Hi {parent.FirstName},\n\n{message}\n\n({note})") ?? "sent";
            if (!string.IsNullOrEmpty(phone))
                smsResult = await _sender.TestSmsAsync(phone, $"{sms} (Aruga survey test)") ?? "sent";

            return Ok(new { title, inApp = "sent", email = emailResult, sms = smsResult });
        }

        private static bool IsEmail(string value)
        {
            try { return new System.Net.Mail.MailAddress(value).Address == value; }
            catch { return false; }
        }
    }
}
