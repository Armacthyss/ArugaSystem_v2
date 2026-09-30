using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // "Notify patients that vaccine stock levels are low so they don't have
    // to waste a trip to the health center" (Chapter 3).
    //
    // A vaccine counts as out of stock when no active, unexpired batch has
    // any doses left. Then every parent of a child due for it within the next
    // week (or already overdue) gets a "don't come for this dose yet" notice.
    // When stock comes back, the same parents get an "available again" notice.
    //
    // Runs every morning with the reminders, and right away when the
    // Admission Staff press "Notify parents" on a stock alert.
    public static class StockNotices
    {
        public const string OutType = "StockOut";
        public const string BackType = "StockBack";

        private const int DaysAhead = 7;
        private const int OverdueDaysBack = 45;

        public static async Task<ParentNotifier.Delivery> RunAsync(
            AppDbContext context, ParentNotifier notifier, int? onlyVaccineId = null)
        {
            var today = DateTime.Today;
            var tally = new ParentNotifier.Delivery();

            var vaccines = await context.Vaccines
                .Where(v => v.Status && (onlyVaccineId == null || v.VaccineID == onlyVaccineId))
                .ToListAsync();

            var inStock = (await context.VaccineInventory
                    .Where(i => i.Status && i.CurrentQuantity > 0 && i.ExpirationDate >= today)
                    .Select(i => i.VaccineID)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            // Latest stock notice of each kind per parent + child + dose
            var history = (await context.Notifications
                    .Where(n => n.ParentID != null && (n.Type == OutType || n.Type == BackType))
                    .Select(n => new { n.ParentID, n.ChildID, n.VaccineID, n.DoseNumber, n.Type, n.CreatedAt })
                    .ToListAsync())
                .GroupBy(n => (n.ParentID, n.ChildID, n.VaccineID, n.DoseNumber))
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Out: g.Where(n => n.Type == OutType).Select(n => (DateTime?)n.CreatedAt).Max(),
                        Back: g.Where(n => n.Type == BackType).Select(n => (DateTime?)n.CreatedAt).Max()));

            string clinicInfo = $"Leveriza Health Center (vaccinations: {await ClinicCalendar.DescribeHoursAsync(context)})";

            foreach (var vaccine in vaccines)
            {
                bool available = inStock.Contains(vaccine.VaccineID);

                var entries = await context.VaccinationTimelines
                    .Include(t => t.Child)
                    .Where(t => t.VaccineID == vaccine.VaccineID
                             && t.Status != "Completed" && t.Status != "Cancelled"
                             && t.ScheduledDate <= today.AddDays(DaysAhead)
                             && t.ScheduledDate >= today.AddDays(-OverdueDaysBack))
                    .ToListAsync();

                foreach (var entry in entries)
                {
                    var child = entry.Child;
                    if (child == null) continue;

                    foreach (var parent in await notifier.ParentsOfChildAsync(child.ChildID))
                    {
                        history.TryGetValue((parent.ParentID, child.ChildID, entry.VaccineID, entry.DoseNumber), out var h);
                        bool toldOut = h.Out != null && (h.Back == null || h.Out > h.Back);

                        if (!available && !toldOut)
                        {
                            await notifier.NotifyAsync(parent, new Notification
                            {
                                ChildID = child.ChildID,
                                VaccineID = entry.VaccineID,
                                DoseNumber = entry.DoseNumber,
                                Type = OutType,
                                Title = $"{vaccine.VaccineName} temporarily unavailable — {child.FirstName}",
                                Message =
                                    $"{vaccine.VaccineName} is out of stock at Leveriza Health Center right now, so " +
                                    $"{child.FirstName}'s Dose {entry.DoseNumber} (due {entry.ScheduledDate:MMMM d}) can't be given yet. " +
                                    "Please hold off on coming in for this dose; we'll message you as soon as it's available again. " +
                                    "Other vaccines that are due can still be given.",
                                ScheduledDate = entry.ScheduledDate,
                            }, tally, smsText:
                                $"Leveriza Health Center: {vaccine.Abbreviation ?? vaccine.VaccineName} is out of stock. " +
                                $"Please don't come for {child.FirstName}'s Dose {entry.DoseNumber} yet; we'll text you when it's back.");
                        }
                        else if (available && toldOut)
                        {
                            await notifier.NotifyAsync(parent, new Notification
                            {
                                ChildID = child.ChildID,
                                VaccineID = entry.VaccineID,
                                DoseNumber = entry.DoseNumber,
                                Type = BackType,
                                Title = $"{vaccine.VaccineName} is available again — {child.FirstName}",
                                Message =
                                    $"Good news! {vaccine.VaccineName} is back in stock. Please bring {child.FirstName} in for " +
                                    $"Dose {entry.DoseNumber} at {clinicInfo}.",
                                ScheduledDate = entry.ScheduledDate,
                            }, tally, smsText:
                                $"Leveriza Health Center: {vaccine.Abbreviation ?? vaccine.VaccineName} is back in stock. " +
                                $"Please bring {child.FirstName} in for Dose {entry.DoseNumber} on the next vaccination day.");
                        }
                    }
                }
            }

            await context.SaveChangesAsync();
            return tally;
        }
    }
}
