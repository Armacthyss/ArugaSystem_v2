using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly ParentNotifier _notifier;

    public NotificationsController(AppDbContext context, AuditService audit, ParentNotifier notifier)
    {
        _context = context;
        _audit = audit;
        _notifier = notifier;
    }

    // GET api/Notifications?from=2026-09-01&to=2026-09-30
    // Admin notification log: every parent- and staff-facing notification,
    // newest first, with recipient/child names resolved. Defaults to the
    // last 60 days.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = (from ?? DateTime.Today.AddDays(-60)).Date;
        var end = (to ?? DateTime.Today).Date.AddDays(1);

        var notifications = await _context.Notifications
            .Where(n => n.CreatedAt >= start && n.CreatedAt < end)
            .OrderByDescending(n => n.CreatedAt)
            .Take(3000)
            .ToListAsync();

        var parentIds = notifications.Where(n => n.ParentID.HasValue).Select(n => n.ParentID!.Value).Distinct().ToList();
        var userIds = notifications.Where(n => n.UserID.HasValue).Select(n => n.UserID!.Value).Distinct().ToList();
        var childIds = notifications.Where(n => n.ChildID.HasValue).Select(n => n.ChildID!.Value).Distinct().ToList();

        var parents = await _context.Parents.Where(p => parentIds.Contains(p.ParentID)).ToDictionaryAsync(p => p.ParentID);
        var users = await _context.Users.Where(u => userIds.Contains(u.UserID)).ToDictionaryAsync(u => u.UserID);
        var children = await _context.Children.Where(c => childIds.Contains(c.ChildID)).ToDictionaryAsync(c => c.ChildID);

        var result = notifications.Select(n =>
        {
            string recipient = "—";
            if (n.ParentID.HasValue && parents.TryGetValue(n.ParentID.Value, out var p))
                recipient = $"{p.FirstName} {p.LastName}".Trim();
            else if (n.UserID.HasValue && users.TryGetValue(n.UserID.Value, out var u))
                recipient = $"{u.FirstName} {u.LastName}".Trim();

            string child = "—";
            if (n.ChildID.HasValue && children.TryGetValue(n.ChildID.Value, out var c))
                child = $"{c.FirstName} {c.LastName}".Trim();

            return new
            {
                notificationID = n.NotificationID,
                title = n.Title,
                message = n.Message,
                type = n.Type,
                category = CategoryOf(n.Type),
                recipient,
                recipientType = n.ParentID.HasValue ? "Parent" : (n.UserID.HasValue ? "Staff" : "—"),
                parentID = n.ParentID,
                userID = n.UserID,
                child,
                childID = n.ChildID,
                scheduledDate = n.ScheduledDate,
                createdAt = n.CreatedAt,
                isRead = n.IsRead,
            };
        });

        return Ok(result);
    }

    // Groups the many internal Type codes into the handful of categories
    // the admin page filters by.
    private static string CategoryOf(string? type)
    {
        if (string.IsNullOrEmpty(type)) return "Other";
        if (type.EndsWith("Resend")) type = type[..^"Resend".Length];
        if (type.StartsWith("Reminder")) return "Vaccination Reminder";
        if (type.StartsWith("Overdue")) return "Missed Vaccination";
        return type switch
        {
            "Completed" => "Vaccination Completed",
            "Announcement" => "Announcement",
            "LowStock" => "Stock Alert",
            "StockOut" or "StockBack" => "Stock Alert",
            "RecordUpdated" => "Record Update",
            "Queue" => "Queue Notification",
            _ => type,
        };
    }

    // GET api/Notifications/channels
    // Which delivery channels are set up on this server (appsettings.json
    // "Email" / "Sms" sections), for the admin Notifications page.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
    [HttpGet("channels")]
    public IActionResult Channels([FromServices] MessageSender sender) => Ok(new
    {
        inApp = true,
        email = sender.EmailEnabled,
        sms = sender.SmsEnabled,
        smsProvider = sender.SmsProvider,
    });

    // POST api/Notifications/broadcast
    // Admin announcement to every parent account, or only the ParentIDs
    // given. Always shows in the parent's notification bell; also goes out
    // by email and/or SMS when SendEmail / SendSms are ticked.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
    [HttpPost("broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Title and message are required." });

        var parentQuery = _context.Parents.AsQueryable();
        if (dto.ParentIDs != null && dto.ParentIDs.Count > 0)
            parentQuery = parentQuery.Where(p => dto.ParentIDs.Contains(p.ParentID));

        var parents = await parentQuery.ToListAsync();
        if (parents.Count == 0)
            return BadRequest(new { message = "No recipients found." });

        var tally = new ParentNotifier.Delivery();
        foreach (var parent in parents)
        {
            await _notifier.NotifyAsync(parent, new Notification
            {
                Type = "Announcement",
                Title = dto.Title.Trim(),
                Message = dto.Message.Trim(),
                ScheduledDate = dto.ScheduledDate,
                IsRead = false,
            }, tally, email: dto.SendEmail, sms: dto.SendSms);
        }

        await _context.SaveChangesAsync();

        await _audit.LogAsync("Notifications", "Create",
            $"Announcement – {dto.Title.Trim()}",
            $"Sent an announcement to {parents.Count} parent(s)" +
            (dto.SendEmail ? $", {tally.Emails} by email" : "") +
            (dto.SendSms ? $", {tally.Texts} by SMS" : "") + ".");

        return Ok(new
        {
            message = "Announcement sent.",
            recipients = parents.Count,
            emails = tally.Emails,
            texts = tally.Texts,
        });
    }

    // POST api/Notifications/stock-notices?vaccineId=5
    // "Notify parents" on a stock alert (Staff Dashboard): tells parents of
    // children due for an out-of-stock vaccine not to come for that dose yet,
    // or that it's available again. Also runs every morning on its own.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
    [HttpPost("stock-notices")]
    public async Task<IActionResult> StockNoticesNow([FromQuery] int? vaccineId)
    {
        var tally = await StockNotices.RunAsync(_context, _notifier, vaccineId);

        if (tally.InApp > 0)
            await _audit.LogAsync("Notifications", "Create", "Stock notice",
                $"Sent {tally.InApp} stock notice(s) to parents.");

        return Ok(new
        {
            message = tally.InApp == 0
                ? "No parents needed a notice: no child is due for an out-of-stock vaccine this week, or they were already told."
                : $"Sent {tally.InApp} notice(s) to parents.",
            sent = tally.InApp,
            emails = tally.Emails,
            texts = tally.Texts,
        });
    }

    // POST api/Notifications/{id}/resend
    // Re-delivers a notification to the same recipient as a new unread copy.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
    [HttpPost("{notificationId:guid}/resend")]
    public async Task<IActionResult> Resend(Guid notificationId)
    {
        var original = await _context.Notifications.FindAsync(notificationId);
        if (original == null) return NotFound(new { message = "Notification not found." });

        var copy = new Notification
        {
            NotificationID = Guid.NewGuid(),
            ParentID = original.ParentID,
            UserID = original.UserID,
            ChildID = original.ChildID,
            VaccineID = original.VaccineID,
            DoseNumber = original.DoseNumber,
            // Keep the same category but not the exact Type, so the daily
            // reminder generator's duplicate check (keyed on Type) still
            // treats the original as the one it sent.
            Type = original.Type == "Announcement" ? "Announcement" : $"{original.Type}Resend",
            Title = original.Title,
            Message = original.Message,
            ScheduledDate = original.ScheduledDate,
            IsRead = false,
            CreatedAt = DateTime.Now,
        };

        _context.Notifications.Add(copy);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("Notifications", "Update", $"Notification – {original.Title}", "Resent a notification.");

        return Ok(new { message = "Notification resent.", notificationID = copy.NotificationID });
    }

    // DELETE api/Notifications/{id}
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) return NotFound(new { message = "Notification not found." });

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("Notifications", "Delete", $"Notification – {notification.Title}", "Deleted a notification.");

        return NoContent();
    }

    // GET api/Notifications/parent/{parentId}
    // All notifications for a parent, newest first
    [HttpGet("parent/{parentId}")]
    public async Task<IActionResult> GetByParent(Guid parentId)
    {
        if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
        var notifications = await _context.Notifications
            .Where(n => n.ParentID == parentId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    // GET api/Notifications/unread-count/{parentId}
    [HttpGet("unread-count/{parentId}")]
    public async Task<IActionResult> GetUnreadCount(Guid parentId)
    {
        if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
        int count = await _context.Notifications
            .CountAsync(n => n.ParentID == parentId && !n.IsRead);
        return Ok(new { count });
    }

    // GET api/Notifications/user/{userId}
    // Staff/doctor-facing equivalent of GetByParent — used by the Doctor
    // dashboard's notification bell. Requires the Notifications.UserID
    // column added in Migration_DoctorModule.sql.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(Guid userId)
    {
        if (AndroidWebAPI.Services.AccessGuard.CallerId(User) != userId && !User.IsInRole(AndroidWebAPI.Services.Roles.Admin)) return Forbid();
        var notifications = await _context.Notifications
            .Where(n => n.UserID == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    // PATCH api/Notifications/mark-all-read/user/{userId}
    // Staff/doctor-facing equivalent of mark-all-read/{parentId}.
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    [HttpPatch("mark-all-read/user/{userId}")]
    public async Task<IActionResult> MarkAllReadForUser(Guid userId)
    {
        if (AndroidWebAPI.Services.AccessGuard.CallerId(User) != userId && !User.IsInRole(AndroidWebAPI.Services.Roles.Admin)) return Forbid();
        var unread = await _context.Notifications
            .Where(n => n.UserID == userId && !n.IsRead)
            .ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _context.SaveChangesAsync();
        return Ok(new { marked = unread.Count });
    }

    // PATCH api/Notifications/mark-read/{notificationId}
    [HttpPatch("mark-read/{notificationId}")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) return NotFound();
        if (notification.ParentID != null && !AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, notification.ParentID.Value)) return Forbid();
        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok();
    }

    // PATCH api/Notifications/mark-all-read/{parentId}
    [HttpPatch("mark-all-read/{parentId}")]
    public async Task<IActionResult> MarkAllRead(Guid parentId)
    {
        if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
        var unread = await _context.Notifications
            .Where(n => n.ParentID == parentId && !n.IsRead)
            .ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _context.SaveChangesAsync();
        return Ok(new { marked = unread.Count });
    }
}


public class BroadcastNotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    // Empty/null = every parent account.
    public List<Guid>? ParentIDs { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public bool SendEmail { get; set; } = true;
    public bool SendSms { get; set; } = false;
}
