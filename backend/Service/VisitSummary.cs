using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // When a visit is finished (Complete Visit, or Staff marking the queue
    // entry Completed), each child's parents get ONE text listing the vaccines
    // given at this visit and the next vaccination date, e.g.
    //   "Leveriza Health Center: Isabela received Penta 2, OPV 2, PCV 2 today.
    //    Next vaccination: Mon, Nov 9 (Penta 3, OPV 3, PCV 3)."
    // The "vaccine administered" notice for each dose still goes to the app
    // and by email as soon as the dose is recorded.
    public static class VisitSummary
    {
        public static async Task<ParentNotifier.Delivery> SendAsync(
            AppDbContext context, ParentNotifier notifier, Guid queueId)
        {
            var tally = new ParentNotifier.Delivery();

            var visit = await context.Queues
                .Include(q => q.QueueChildren).ThenInclude(qc => qc.Child)
                .FirstOrDefaultAsync(q => q.QueueID == queueId);
            if (visit == null) return tally;

            var day = visit.QueueDate.Date;

            foreach (var child in visit.QueueChildren.Select(qc => qc.Child).Where(c => c != null))
            {
                var given = await context.VaccinationRecords
                    .Include(r => r.Vaccine)
                    .Where(r => r.ChildID == child!.ChildID && r.Status == "Completed"
                             && r.VaccinationDate >= day && r.VaccinationDate < day.AddDays(1))
                    .OrderBy(r => r.VaccineID)
                    .ToListAsync();
                if (given.Count == 0) continue;   // nothing given (e.g. deferred): no text

                var upcoming = await context.VaccinationTimelines
                    .Include(t => t.Vaccine)
                    .Where(t => t.ChildID == child!.ChildID && t.Status == "Pending" && t.ScheduledDate > day)
                    .OrderBy(t => t.ScheduledDate)
                    .ToListAsync();

                string givenList = string.Join(", ", given.Select(r => $"{r.Vaccine?.Abbreviation ?? r.Vaccine?.VaccineName} {r.DoseNumber}"));
                string next = "All of the scheduled vaccines are done.";
                if (upcoming.Count > 0)
                {
                    var nextDate = upcoming[0].ScheduledDate.Date;
                    var nextList = upcoming.Where(t => t.ScheduledDate.Date == nextDate)
                        .Select(t => $"{t.Vaccine?.Abbreviation ?? t.Vaccine?.VaccineName} {t.DoseNumber}");
                    next = $"Next vaccination: {nextDate:ddd, MMM d} ({string.Join(", ", nextList)}).";
                }

                string text = $"Leveriza Health Center: {child!.FirstName} received {givenList} today. {next}";
                foreach (var parent in await notifier.ParentsOfChildAsync(child.ChildID))
                    await notifier.TextAsync(parent, text, tally);
            }

            return tally;
        }
    }
}
