using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // When the admin closes the health center on one or more dates (a
    // typhoon, a holiday) under Operating Hours, parents are told:
    //   - every parent of a registered child gets the notice in the app;
    //   - parents whose child was due on one of those dates also get it by
    //     email and SMS, with the child's new date (the doses have already
    //     been moved to the next vaccination day).
    // Email and SMS go only to the families who have to change plans, so one
    // closure doesn't use up the free SMS plan. The Announcements page can
    // still send a message to everyone by email and SMS.
    public static class ClosureNotices
    {
        public const string Type = "ClinicClosed";

        // Doses due on these dates. Call BEFORE the doses are moved.
        public static Task<List<Guid>> DosesDueOnAsync(AppDbContext context, IEnumerable<DateTime> dates)
        {
            var days = dates.Select(d => d.Date).ToList();
            return context.VaccinationTimelines
                .Where(t => t.Status == "Pending" && days.Contains(t.ScheduledDate.Date))
                .Select(t => t.TimelineID)
                .ToListAsync();
        }

        public static async Task<ParentNotifier.Delivery> SendAsync(
            AppDbContext context, ParentNotifier notifier,
            IEnumerable<DateTime> closedDates, string? reason, List<Guid> dosesDueThen)
        {
            var tally = new ParentNotifier.Delivery();
            var today = DateTime.Today;

            // Past dates are just record-keeping; nobody needs to be told.
            var dates = closedDates.Select(d => d.Date).Where(d => d >= today).Distinct().OrderBy(d => d).ToList();
            if (dates.Count == 0) return tally;

            string when = Describe(dates, "dddd, MMMM d");        // "from Monday, September 28 to Tuesday, September 29"
            string whenShort = Describe(dates, "ddd, MMM d");     // "from Mon, Sep 28 to Tue, Sep 29"
            string why = string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason.Trim()})";
            var reopens = await ClinicCalendar.NextOpenDayAsync(context, dates.Last().AddDays(1));

            string title = "Health center closed: " + (dates.Count == 1
                ? dates[0].ToString("ddd, MMM d")
                : $"{dates[0]:ddd, MMM d} - {dates[^1]:ddd, MMM d}");
            string notice =
                $"Leveriza Health Center is closed {when}{why}. " +
                $"There are no vaccinations or check-in on {(dates.Count == 1 ? "that day" : "those days")}. " +
                $"Vaccinations resume on {reopens:dddd, MMMM d}.";

            // Families with a dose that was due on a closed date, told the new date
            var moved = await context.VaccinationTimelines
                .Include(t => t.Child)
                .Include(t => t.Vaccine)
                .Where(t => dosesDueThen.Contains(t.TimelineID))
                .ToListAsync();

            var byParent = new Dictionary<Guid, (Parent Parent, List<VaccinationTimeline> Doses)>();
            foreach (var childDoses in moved.Where(t => t.Child != null).GroupBy(t => t.ChildID))
            {
                foreach (var parent in await notifier.ParentsOfChildAsync(childDoses.Key))
                {
                    if (!byParent.TryGetValue(parent.ParentID, out var entry))
                        byParent[parent.ParentID] = entry = (parent, new List<VaccinationTimeline>());
                    entry.Doses.AddRange(childDoses);
                }
            }

            foreach (var (parent, doses) in byParent.Values)
            {
                var lines = doses
                    .OrderBy(t => t.Child!.FirstName).ThenBy(t => t.ScheduledDate)
                    .Select(t => $"{t.Child!.FirstName}'s {t.Vaccine?.VaccineName ?? "vaccine"} (Dose {t.DoseNumber}) is moved to {t.ScheduledDate:dddd, MMMM d}.");

                var children = doses.Select(t => t.Child!).DistinctBy(c => c.ChildID).ToList();
                var newDates = doses.Select(t => t.ScheduledDate.Date).Distinct().OrderBy(d => d).ToList();
                string movedText = children.Count == 1 && newDates.Count == 1
                    ? $"{children[0].FirstName}'s {string.Join(", ", doses.Select(t => t.Vaccine?.Abbreviation ?? t.Vaccine?.VaccineName).Distinct())} moved to {newDates[0]:ddd, MMM d}."
                    : "Your child's vaccines are moved; see the Aruga app for the new dates.";

                await notifier.NotifyAsync(parent, new Notification
                {
                    ChildID = children.Count == 1 ? children[0].ChildID : null,
                    Type = Type,
                    Title = title,
                    Message = $"{notice}\n\n{string.Join("\n", lines)}",
                    ScheduledDate = newDates[0],
                }, tally, smsText: $"Leveriza Health Center is CLOSED {whenShort}{why}. {movedText}");
            }

            // Everyone else with a registered child: in the app only
            var parentIds = await context.ChildParentRelationships
                .Where(r => r.Status == "Active" && (r.CanReceiveNotifications || r.IsPrimaryContact))
                .Select(r => r.ParentID)
                .Distinct()
                .ToListAsync();
            parentIds = parentIds.Where(id => !byParent.ContainsKey(id)).ToList();

            foreach (var parent in await context.Parents.Where(p => parentIds.Contains(p.ParentID)).ToListAsync())
            {
                await notifier.NotifyAsync(parent, new Notification
                {
                    Type = Type,
                    Title = title,
                    Message = notice,
                    ScheduledDate = dates[0],
                }, tally, email: false, sms: false);
            }

            await context.SaveChangesAsync();
            return tally;
        }

        // "on Monday, September 28" or "from Monday, September 28 to Wednesday, September 30"
        private static string Describe(List<DateTime> dates, string format)
        {
            if (dates.Count == 1) return "on " + dates[0].ToString(format);
            bool consecutive = dates.Zip(dates.Skip(1), (a, b) => (b - a).Days == 1).All(x => x);
            return consecutive
                ? $"from {dates[0].ToString(format)} to {dates[^1].ToString(format)}"
                : "on " + string.Join(", ", dates.Select(d => d.ToString(format)));
        }
    }
}
