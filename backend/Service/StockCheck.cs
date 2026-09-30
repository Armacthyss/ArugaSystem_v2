using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Weekly stock check (Leveriza checks its vaccine storage every Wednesday).
    //
    // For each vaccine: doses on hand (active, unexpired batches), doses the
    // children need this week and over the next two weeks (from the
    // vaccination timeline, overdue doses included), batches about to expire,
    // and whether to re-stock. The suggested order covers two weeks plus the
    // minimum stock, because when the pharmacy is out the supplies only come
    // the following week.
    //
    // The Admission Staff (who call the pharmacy) and the Administrator get it
    // in their notification bell and by email every Wednesday at 8:00 AM
    // (Inventory:StockCheckDay in appsettings.json changes the day). The same
    // table is on the Staff Inventory page at any time.
    public static class StockCheck
    {
        public const string NotificationType = "StockCheck";

        public class Line
        {
            public int VaccineID { get; set; }
            public string Vaccine { get; set; } = "";
            public string Short { get; set; } = "";
            public int OnHand { get; set; }
            public int Minimum { get; set; }
            public int DueThisWeek { get; set; }
            public int DueTwoWeeks { get; set; }
            public int ExpiringSoon { get; set; }
            public DateTime? NextExpiry { get; set; }
            public string Status { get; set; } = "OK";      // OK / Low / Not enough this week / Out of stock
            public bool Restock { get; set; }
            public int SuggestedOrder { get; set; }
        }

        public static async Task<List<Line>> BuildAsync(AppDbContext context)
        {
            var today = DateTime.Today;
            var vaccines = await context.Vaccines.Where(v => v.Status).OrderBy(v => v.VaccineID).ToListAsync();
            var batches = await context.VaccineInventory
                .Where(i => i.Status && i.ExpirationDate >= today)
                .ToListAsync();
            var due = await context.VaccinationTimelines
                .Where(t => t.Status != "Completed" && t.Status != "Cancelled"
                         && t.ScheduledDate >= today.AddDays(-45) && t.ScheduledDate <= today.AddDays(14))
                .Select(t => new { t.VaccineID, t.ScheduledDate })
                .ToListAsync();

            var lines = new List<Line>();
            foreach (var v in vaccines)
            {
                var mine = batches.Where(b => b.VaccineID == v.VaccineID).ToList();
                int onHand = mine.Sum(b => b.CurrentQuantity);
                // Same rule as the Staff Dashboard's stock alerts
                int minimum = mine.Sum(b => b.MinimumStock);
                if (minimum == 0) minimum = 20;
                int thisWeek = due.Count(d => d.VaccineID == v.VaccineID && d.ScheduledDate <= today.AddDays(7));
                int twoWeeks = due.Count(d => d.VaccineID == v.VaccineID);
                var expiring = mine.Where(b => b.CurrentQuantity > 0 && b.ExpirationDate <= today.AddDays(30)).ToList();

                string status =
                    onHand == 0 ? "Out of stock" :
                    onHand < thisWeek ? "Not enough this week" :
                    onHand < minimum ? "Low" : "OK";
                bool restock = status != "OK";

                lines.Add(new Line
                {
                    VaccineID = v.VaccineID,
                    Vaccine = v.VaccineName,
                    Short = string.IsNullOrWhiteSpace(v.Abbreviation) ? v.VaccineName : v.Abbreviation!,
                    OnHand = onHand,
                    Minimum = minimum,
                    DueThisWeek = thisWeek,
                    DueTwoWeeks = twoWeeks,
                    ExpiringSoon = expiring.Sum(b => b.CurrentQuantity),
                    NextExpiry = expiring.Count > 0 ? expiring.Min(b => b.ExpirationDate) : null,
                    Status = status,
                    Restock = restock,
                    SuggestedOrder = restock ? Math.Max(0, twoWeeks + minimum - onHand) : 0,
                });
            }
            return lines;
        }

        public static DayOfWeek CheckDay(IConfiguration config) =>
            Enum.TryParse<DayOfWeek>(config["Inventory:StockCheckDay"], true, out var d) ? d : DayOfWeek.Wednesday;

        // Sends the check to every active Admission Staff and Administrator
        // (bell + email). Once a day at most; returns how many were notified.
        public static async Task<int> SendAsync(AppDbContext context, MessageSender sender)
        {
            var today = DateTime.Today;
            bool alreadySent = await context.Notifications
                .AnyAsync(n => n.Type == NotificationType && n.CreatedAt >= today && n.CreatedAt < today.AddDays(1));
            if (alreadySent) return 0;

            var lines = await BuildAsync(context);
            var restock = lines.Where(l => l.Restock).ToList();
            var expiring = lines.Where(l => l.ExpiringSoon > 0).ToList();

            string title = restock.Count == 0
                ? $"{today:dddd} stock check: all vaccines have enough"
                : $"{today:dddd} stock check: {restock.Count} vaccine{(restock.Count == 1 ? "" : "s")} to re-stock";

            // Short version for the bell (500 characters max)
            string bell = restock.Count == 0
                ? "Every vaccine has enough doses for this week's schedule and the minimum stock."
                : "Re-stock today (call the pharmacy): " +
                  string.Join("; ", restock.Select(l => $"{l.Short}: {l.OnHand} left, {l.DueThisWeek} due this week, order about {l.SuggestedOrder}")) + ".";
            if (expiring.Count > 0)
                bell += " Expiring within 30 days: " + string.Join(", ", expiring.Select(l => $"{l.Short} ({l.ExpiringSoon})")) + ".";
            bell += " Details: Inventory page.";

            // Full version for email
            var body = new System.Text.StringBuilder();
            body.AppendLine($"Weekly vaccine stock check for {today:dddd, MMMM d, yyyy}.");
            body.AppendLine();
            if (restock.Count > 0)
            {
                body.AppendLine("RE-STOCK TODAY (call the pharmacy):");
                foreach (var l in restock)
                    body.AppendLine($"• {l.Vaccine}: {l.Status.ToLower()}. {l.OnHand} doses on hand, {l.DueThisWeek} due this week, " +
                                    $"{l.DueTwoWeeks} in the next two weeks (minimum {l.Minimum}). Order about {l.SuggestedOrder} doses.");
                body.AppendLine("If the pharmacy is out of stock too, the supplies come next week. The suggested amounts cover two weeks for that reason.");
                body.AppendLine();
            }
            var ok = lines.Where(l => !l.Restock).ToList();
            if (ok.Count > 0)
            {
                body.AppendLine("ENOUGH FOR NOW:");
                foreach (var l in ok)
                    body.AppendLine($"• {l.Vaccine}: {l.OnHand} doses on hand, {l.DueThisWeek} due this week.");
                body.AppendLine();
            }
            if (expiring.Count > 0)
            {
                body.AppendLine("USE FIRST (expiring within 30 days):");
                foreach (var l in expiring)
                    body.AppendLine($"• {l.Vaccine}: {l.ExpiringSoon} doses, earliest expiry {l.NextExpiry:MMMM d}.");
                body.AppendLine();
            }
            body.AppendLine("After the delivery arrives, add it under Inventory → Receive New Batch. Parents who were told a vaccine was out of stock are notified automatically.");

            var recipients = await context.Users
                .Where(u => u.AccountStatus == "Active"    // every Doctor and Nurse
                         && (u.Position == null || u.Position != Roles.SuperAdminPosition))
                .ToListAsync();

            foreach (var u in recipients)
            {
                var n = new Notification
                {
                    NotificationID = Guid.NewGuid(),
                    UserID = u.UserID,
                    Type = NotificationType,
                    Title = title,
                    Message = bell,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                };
                ParentNotifier.FitToColumns(n);
                context.Notifications.Add(n);
            }
            await context.SaveChangesAsync();

            foreach (var u in recipients)
                await sender.SendEmailAsync(u.Email, title, $"Hi {u.FirstName},\n\n{body}");

            return recipients.Count;
        }
    }
}
