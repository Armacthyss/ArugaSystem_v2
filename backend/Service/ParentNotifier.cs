using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // One place that tells a parent something through every channel the
    // study promises: the in-app notification bell, email, and SMS.
    //
    // The in-app notification is added to the DbContext; the CALLER saves it
    // (so it goes in the same SaveChanges as the rest of its work). Email and
    // SMS go out straight away, through MessageSender.
    public class ParentNotifier
    {
        private readonly AppDbContext _context;
        private readonly MessageSender _sender;

        public ParentNotifier(AppDbContext context, MessageSender sender)
        {
            _context = context;
            _sender = sender;
        }

        public class Delivery
        {
            public int InApp { get; set; }
            public int Emails { get; set; }
            public int Texts { get; set; }
        }

        // Parents who should hear about this child: active links that allow
        // notifications. Falls back to the primary contact.
        public async Task<List<Parent>> ParentsOfChildAsync(Guid childId)
        {
            var links = await _context.ChildParentRelationships
                .Include(r => r.Parent)
                .Where(r => r.ChildID == childId && r.Status == "Active")
                .ToListAsync();

            var chosen = links.Where(r => r.CanReceiveNotifications).ToList();
            if (chosen.Count == 0) chosen = links.Where(r => r.IsPrimaryContact).ToList();
            return chosen.Select(r => r.Parent).DistinctBy(p => p.ParentID).ToList();
        }

        // In-app + email + SMS to one parent.
        public async Task NotifyAsync(
            Parent parent,
            Notification inApp,
            Delivery tally,
            bool email = true,
            bool sms = true,
            string? smsText = null)
        {
            inApp.NotificationID = inApp.NotificationID == Guid.Empty ? Guid.NewGuid() : inApp.NotificationID;
            inApp.ParentID = parent.ParentID;
            inApp.CreatedAt = DateTime.Now;
            string fullMessage = inApp.Message;
            FitToColumns(inApp);
            _context.Notifications.Add(inApp);
            tally.InApp++;

            string text = smsText ?? $"Aruga - Leveriza Health Center: {inApp.Title}. {fullMessage}";

            // Demo parents (DemoSeed.sql, IDs starting A2A2) and test records
            // (…@example.com, …@demo.…) have made-up emails and numbers that
            // could belong to real people, so they only get the in-app notice.
            // The text they would get is written to the console, to check it.
            if (IsDemo(parent))
            {
                if (sms) await _sender.SendSmsAsync(parent.ContactNo, text, demoRecipient: true);
                return;
            }

            if (email && !string.IsNullOrWhiteSpace(parent.Email) &&
                await _sender.SendEmailAsync(parent.Email, inApp.Title, $"Hi {parent.FirstName},\n\n{fullMessage}"))
                tally.Emails++;

            if (sms && await _sender.SendSmsAsync(parent.ContactNo, text))
                tally.Texts++;
        }

        // A text only, for summaries that combine several app/email notices
        // into one SMS (reminders for a whole visit, the visit summary). Demo
        // and test parents are skipped as in NotifyAsync; the text is written
        // to the console instead, so it can still be checked.
        public async Task TextAsync(Parent parent, string text, Delivery tally)
        {
            bool demo = IsDemo(parent);
            if (await _sender.SendSmsAsync(parent.ContactNo, text, demoRecipient: demo) && !demo)
                tally.Texts++;
        }

        public static bool IsDemo(Parent parent) =>
            parent.ParentID.ToString().StartsWith("a2a20000-", StringComparison.OrdinalIgnoreCase) ||
            MessageSender.IsTestAddress(parent.Email);

        // Notifications.Title holds 200 characters and Message 500; a longer
        // text would make the whole save fail, so the in-app copy is shortened
        // (emails still get the full text).
        public static void FitToColumns(Notification n)
        {
            if (n.Title.Length > 200) n.Title = n.Title[..197] + "...";
            if (n.Message.Length > 500) n.Message = n.Message[..497] + "...";
        }
    }
}
