using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Inventory summary per vaccine, for the Inventory pages and the
    // Inventory Reports (beneficiary request, Sep 2026):
    //   on hand, minimum, doses received and used in the period, expiring
    //   within 30 days, expired doses still on the shelf, the average doses
    //   used per week (last 8 weeks) and about how many weeks the stock lasts.
    // "Used" counts vaccinations recorded from a clinic batch (a historical
    // Yellow Book record has no batch and doesn't use stock).
    public static class InventorySummary
    {
        public record Line(
            int VaccineID,
            string VaccineName,
            string? Abbreviation,
            int OnHand,
            int MinimumStock,
            int ActiveBatches,
            int ReceivedInPeriod,
            int UsedInPeriod,
            int ExpiringSoon,
            int ExpiredOnShelf,
            DateTime? NextExpiry,
            double AverageWeeklyUse,
            double? WeeksLeft,
            string Status);

        public static async Task<List<Line>> BuildAsync(AppDbContext context, DateTime from, DateTime to)
        {
            var today = DateTime.Today;
            var start = from.Date;
            var end = to.Date.AddDays(1);
            var eightWeeksAgo = today.AddDays(-56);

            var vaccines = await context.Vaccines.AsNoTracking().OrderBy(v => v.VaccineID).ToListAsync();
            var batches = await context.VaccineInventory.AsNoTracking().ToListAsync();

            // Doses given from a clinic batch, per vaccine
            var given = await context.VaccinationRecords.AsNoTracking()
                .Where(r => r.Status == "Completed" && r.InventoryID != null)
                .Select(r => new { r.VaccineID, r.VaccinationDate })
                .ToListAsync();

            var lines = new List<Line>();
            foreach (var v in vaccines)
            {
                var mine = batches.Where(b => b.VaccineID == v.VaccineID).ToList();
                var usable = mine.Where(b => b.Status && b.ExpirationDate.Date >= today).ToList();

                // Vaccines the clinic doesn't stock (no batches, inactive) are left out
                if (mine.Count == 0 && !v.Status) continue;

                int onHand = usable.Sum(b => b.CurrentQuantity);
                int minimum = usable.Sum(b => b.MinimumStock);
                if (minimum == 0) minimum = mine.Select(b => b.MinimumStock).DefaultIfEmpty(0).Max();

                int used8Weeks = given.Count(g => g.VaccineID == v.VaccineID && g.VaccinationDate >= eightWeeksAgo && g.VaccinationDate < today.AddDays(1));
                double weekly = Math.Round(used8Weeks / 8.0, 1);

                lines.Add(new Line(
                    v.VaccineID,
                    v.VaccineName,
                    v.Abbreviation,
                    onHand,
                    minimum,
                    usable.Count(b => b.CurrentQuantity > 0),
                    mine.Where(b => b.ReceivedDate >= start && b.ReceivedDate < end).Sum(b => b.InitialQuantity),
                    given.Count(g => g.VaccineID == v.VaccineID && g.VaccinationDate >= start && g.VaccinationDate < end),
                    usable.Where(b => b.CurrentQuantity > 0 && (b.ExpirationDate.Date - today).TotalDays <= 30).Sum(b => b.CurrentQuantity),
                    mine.Where(b => b.Status && b.ExpirationDate.Date < today).Sum(b => b.CurrentQuantity),
                    usable.Where(b => b.CurrentQuantity > 0).Select(b => (DateTime?)b.ExpirationDate).Min(),
                    weekly,
                    weekly > 0 ? Math.Round(onHand / weekly, 1) : null,
                    onHand == 0 ? "Out of Stock" : onHand < minimum ? "Low Stock" : "Good"));
            }
            return lines;
        }
    }
}
