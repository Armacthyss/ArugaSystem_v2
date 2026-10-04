// Services/NotificationGeneratorService.cs
//
// Register in Program.cs with:
//   builder.Services.AddHostedService<NotificationGeneratorService>();
//
// Runs once at startup (if it's between 8 AM and 8 PM), then every day at
// DailyRunHour (server local time).
// PRE-DUE:  reminders 14, 7, 5, 3 and 1 day(s) before each scheduled dose.
// POST-DUE: overdue follow-ups 1, 5, 14 and 30 days after a missed dose.
// STOCK:    if a vaccine is out of stock, parents of children due for it in
//           the next 7 days are told not to come for that dose yet, and are
//           told again once it's back in stock (see StockNotices.cs).
//
// Every notice goes to the parent's notification bell and by email, one per
// dose, to every linked parent who accepts notifications. Texts are combined:
// one SMS per child per reminder step, listing that visit's vaccines.
//
// Due dates come from dbo.VaccinationTimeline, the same schedule parents
// and health workers see in the app (it is recalculated whenever a dose is
// recorded), and the clinic's days/hours in the messages come from the
// Operating Hours the admin sets.
//
// For each dose only ONE notification is created per run: the tightest
// threshold the dose has reached that hasn't been sent yet. That prevents a
// burst of stale reminders (e.g. "in 1 month" + "tomorrow") when a child is
// first processed close to, or after, the due date.

using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class NotificationGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationGeneratorService> _logger;

    // Days BEFORE the due date to send pre-reminders
    private static readonly int[] PreReminders = { 14, 7, 5, 3, 1 };

    // Days AFTER the due date to send overdue follow-ups
    private static readonly int[] PostReminders = { 1, 5, 14, 30 };

    // Overdue follow-ups stop after this many days: the 30-day notice is the last
    private const int StopAfterDaysLate = 45;

    // Hour of day (0-23, server local time) for the daily run
    private const int DailyRunHour = 8;

    // From this hour until the next morning run, nothing is sent: when the API
    // is (re)started at night, parents shouldn't get texts in the middle of it.
    private const int QuietFromHour = 20;

    // The daily run and "Send due reminders now" (Admin) never run at the
    // same time, so the duplicate check always sees what the other one sent
    private static readonly SemaphoreSlim RunLock = new(1, 1);

    // Admin "Send due reminders now": the same run as every morning, right
    // away. Reminders already sent are never sent again.
    public async Task<ParentNotifier.Delivery> RunNowAsync()
    {
        await RunLock.WaitAsync();
        try { return await GenerateNotificationsAsync(); }
        finally { RunLock.Release(); }
    }

    public NotificationGeneratorService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int hour = DateTime.Now.Hour;
                await RunLock.WaitAsync(stoppingToken);
                try
                {
                    if (hour >= DailyRunHour && hour < QuietFromHour)
                        await GenerateNotificationsAsync();
                    else
                        await UpdateMissedAsync();   // messages wait for the morning run
                }
                finally { RunLock.Release(); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating notifications");
            }

            // Wait until the next daily run time instead of "24h from startup"
            var now = DateTime.Now;
            var next = now.Date.AddHours(DailyRunHour);
            if (next <= now) next = next.AddDays(1);
            await Task.Delay(next - now, stoppingToken);
        }
    }

    private async Task UpdateMissedAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IVaccinationTimelineRepository>().UpdateMissedVaccinationsAsync();
    }

    private async Task<ParentNotifier.Delivery> GenerateNotificationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<ParentNotifier>();

        // Doses whose day has passed without being given become "Missed".
        await scope.ServiceProvider.GetRequiredService<IVaccinationTimelineRepository>().UpdateMissedVaccinationsAsync();

        var today = DateTime.Today;
        string clinicHoursShort = await ClinicCalendar.DescribeHoursAsync(context);
        string clinicInfo = $"Leveriza Health Center (vaccinations: {clinicHoursShort})";

        // Doses still to be given, from the live timeline
        var due = await context.VaccinationTimelines
            .Include(t => t.Child)
            .Include(t => t.Vaccine)
            .Where(t => t.Status != "Completed" && t.Status != "Cancelled"
                     && t.ScheduledDate <= today.AddDays(PreReminders.Max())
                     && t.ScheduledDate >= today.AddDays(-StopAfterDaysLate))
            .OrderBy(t => t.ScheduledDate).ThenBy(t => t.VaccineID).ThenBy(t => t.DoseNumber)
            .ToListAsync();

        // The earliest dose of each vaccine a child still hasn't had. A later
        // dose isn't reminded about while an earlier one is missing (Penta 2
        // can't be given before Penta 1); the overdue reminder covers it.
        var firstOpenDose = (await context.VaccinationTimelines
                .Where(t => t.Status == "Pending" || t.Status == "Missed")
                .GroupBy(t => new { t.ChildID, t.VaccineID })
                .Select(g => new { g.Key.ChildID, g.Key.VaccineID, Dose = g.Min(t => t.DoseNumber) })
                .ToListAsync())
            .ToDictionary(x => (x.ChildID, x.VaccineID), x => x.Dose);

        // Keys of notifications that already exist, for fast duplicate checks
        var sentKeys = (await context.Notifications
                .Where(n => n.ParentID != null)
                .Select(n => new { n.ParentID, n.ChildID, n.VaccineID, n.DoseNumber, n.Type })
                .ToListAsync())
            .Select(n => Key(n.ParentID, n.ChildID, n.VaccineID, n.DoseNumber, n.Type))
            .ToHashSet();

        var tally = new ParentNotifier.Delivery();
        var parentsByChild = new Dictionary<Guid, List<Parent>>();

        // Texts: ONE per parent, child and reminder step, listing every vaccine
        // of that visit ("Penta 2, OPV 2, PCV 2") instead of one text per dose.
        var texts = new Dictionary<(Guid Parent, Guid Child, string Type, DateTime Date), SmsReminder>();

        foreach (var entry in due)
        {
            var child = entry.Child;
            if (child == null) continue;

            if (firstOpenDose.TryGetValue((entry.ChildID, entry.VaccineID), out var firstDose) && entry.DoseNumber > firstDose)
                continue;

            if (!parentsByChild.TryGetValue(child.ChildID, out var parents))
                parentsByChild[child.ChildID] = parents = await notifier.ParentsOfChildAsync(child.ChildID);
            if (parents.Count == 0) continue;

            string childName = $"{child.FirstName} {child.LastName}";
            string vaccineName = entry.Vaccine?.VaccineName ?? $"Vaccine {entry.VaccineID}";
            string doseLabel = $"Dose {entry.DoseNumber}";
            string dateLabel = entry.ScheduledDate.ToString("MMMM d, yyyy");

            // Positive = days until due, negative = days overdue
            int daysUntil = (entry.ScheduledDate.Date - today).Days;

            string type, title, message;

            if (daysUntil >= 1)
            {
                // ── PRE-DUE ──────────────────────────────────────────────
                // Tightest threshold reached: e.g. 10 days out -> the 14-day
                // reminder; 4 days out -> the 5-day reminder. 0 = too early.
                int threshold = PreReminders
                    .Where(d => d >= daysUntil)
                    .DefaultIfEmpty(0)
                    .Min();

                if (threshold == 0) continue;

                string timeLabel = daysUntil == 1 ? "tomorrow" : $"in {daysUntil} days";
                type = PreDueType(threshold);
                title = $"Vaccine due {timeLabel} — {childName}";
                message = PreDueMessage(threshold, daysUntil, vaccineName, doseLabel, childName, dateLabel, clinicInfo);
            }
            else if (daysUntil < 0)
            {
                // ── POST-DUE ─────────────────────────────────────────────
                int daysLate = -daysUntil;

                // Latest overdue threshold reached (0 = none yet)
                int threshold = PostReminders
                    .Where(d => d <= daysLate)
                    .DefaultIfEmpty(0)
                    .Max();

                if (threshold == 0) continue;

                type = OverdueType(threshold);
                (title, message) = OverdueText(
                    threshold, daysLate, vaccineName, doseLabel,
                    childName, child.FirstName, dateLabel, clinicInfo);
            }
            else
            {
                // daysUntil == 0 (due today): the "tomorrow" reminder went out
                // yesterday and overdue follow-ups start tomorrow.
                continue;
            }

            // Each dose gets its own notice in the app and by email; the text
            // for the whole visit is sent after the loop.
            foreach (var parent in parents)
            {
                if (!sentKeys.Add(Key(parent.ParentID, child.ChildID, entry.VaccineID, entry.DoseNumber, type)))
                    continue;

                await notifier.NotifyAsync(parent, new Notification
                {
                    ChildID       = child.ChildID,
                    VaccineID     = entry.VaccineID,
                    DoseNumber    = entry.DoseNumber,
                    Type          = type,
                    Title         = title,
                    Message       = message,
                    ScheduledDate = entry.ScheduledDate,
                    IsRead        = false,
                }, tally, sms: false);

                var textKey = (parent.ParentID, child.ChildID, type, entry.ScheduledDate.Date);
                if (!texts.TryGetValue(textKey, out var text))
                    texts[textKey] = text = new SmsReminder(parent, child, type, entry.ScheduledDate.Date, daysUntil);
                text.Vaccines.Add($"{entry.Vaccine?.Abbreviation ?? vaccineName} {entry.DoseNumber}");
            }
        }

        await context.SaveChangesAsync();

        // Most urgent first ("due tomorrow", "missed yesterday"...), so if the
        // daily SMS limit is reached it's the 14- and 30-day texts that wait.
        foreach (var text in texts.Values.OrderBy(t => Math.Abs(t.DaysUntil)).ThenBy(t => t.DaysUntil))
            await notifier.TextAsync(text.Parent, text.Build(clinicHoursShort), tally);

        var stock = await StockNotices.RunAsync(context, notifier);

        // Weekly stock check for the Admission Staff and Administrator
        // (Wednesdays unless Inventory:StockCheckDay says otherwise)
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        int stockCheck = 0;
        if (today.DayOfWeek == StockCheck.CheckDay(config))
            stockCheck = await StockCheck.SendAsync(context, scope.ServiceProvider.GetRequiredService<MessageSender>());

        _logger.LogInformation(
            "Notifications generated at {Time}: {Count} reminders ({Emails} emails, {Texts} SMS), {Stock} stock notices, stock check sent to {Check}",
            DateTime.Now, tally.InApp, tally.Emails, tally.Texts, stock.InApp, stockCheck);
        return tally;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Key(Guid? parentId, Guid? childId, int? vaccineId, int? doseNumber, string type)
        => $"{parentId}|{childId}|{vaccineId}|{doseNumber}|{type}";

    // One reminder text: a child's vaccines due (or missed) on one date.
    private sealed class SmsReminder(Parent parent, Child child, string type, DateTime date, int daysUntil)
    {
        public Parent Parent { get; } = parent;
        public int DaysUntil { get; } = daysUntil;
        public List<string> Vaccines { get; } = new();

        // e.g. "Leveriza Health Center: Isabela's vaccines (Penta 2, OPV 2, PCV 2)
        // are due in 7 days, Mon, Oct 12. Vaccinations: Mon, Wed, Fri, 8:00 AM - 12:00 PM."
        public string Build(string hours)
        {
            string list = string.Join(", ", Vaccines);
            bool many = Vaccines.Count > 1;
            string name = child.FirstName;

            if (DaysUntil >= 1)
            {
                string when = DaysUntil == 1 ? "tomorrow" : $"in {DaysUntil} days";
                return $"Leveriza Health Center: {name}'s {(many ? "vaccines" : "vaccine")} ({list}) {(many ? "are" : "is")} due {when}, {date:ddd, MMM d}. Vaccinations: {hours}.";
            }

            int daysLate = -DaysUntil;
            return type == "OverdueMiss"
                ? $"Leveriza Health Center: {name} missed {list} on {date:ddd, MMM d}. Please come on the next vaccination day ({hours})."
                : $"Leveriza Health Center: {name}'s {list} {(many ? "are" : "is")} {daysLate} days overdue (due {date:MMM d}). Please come on the next vaccination day ({hours}).";
        }
    }

    private static string PreDueType(int daysBefore) => daysBefore switch
    {
        14 => "Reminder2Week",
        7  => "ReminderWeek",
        5  => "Reminder5Day",
        3  => "Reminder3Day",
        1  => "ReminderDay",
        _  => "Reminder"
    };

    private static string OverdueType(int daysAfter) => daysAfter switch
    {
        1  => "OverdueMiss",
        5  => "Overdue5Day",
        14 => "Overdue2Week",
        30 => "OverdueUrgent",
        _  => "Overdue"
    };

    // Wording follows the threshold (tone) but uses the REAL number of days left
    private static string PreDueMessage(
        int threshold, int daysUntil, string vaccine, string dose, string childName, string dateLabel, string clinicInfo)
    {
        string what = $"{vaccine} ({dose}) for {childName}";

        return threshold switch
        {
            14 => $"Heads up! {what} is due on {dateLabel}, about {daysUntil} days from now. " +
                  $"Please start planning your visit to {clinicInfo}.",
            7  => $"{what} is due on {dateLabel} — {daysUntil} days away. " +
                  $"Make sure to visit {clinicInfo}.",
            5  => $"Don't forget! {what} is due on {dateLabel}. Only {daysUntil} days left. " +
                  $"Visit {clinicInfo}.",
            3  => $"{what} is due on {dateLabel} — just {daysUntil} days away. " +
                  $"Please get ready to visit {clinicInfo}.",
            1  => $"Reminder: {what} is scheduled TOMORROW, {dateLabel}. " +
                  $"Please visit {clinicInfo}.",
            _  => $"{what} is due on {dateLabel}."
        };
    }

    // Survey mode "Send my notifications to me": the reminder the parent
    // would get for this dose, in the same words as the daily reminders above
    // (app/email title + message, and the text message).
    internal static (string Type, string Title, string Message, string Sms) SampleReminder(
        string vaccineName, string? abbreviation, int doseNumber, string childFirst, string childLast,
        DateTime scheduled, DateTime today, string clinicHoursShort)
    {
        string childName = $"{childFirst} {childLast}";
        string doseLabel = $"Dose {doseNumber}";
        string dateLabel = scheduled.ToString("MMMM d, yyyy");
        string clinicInfo = $"Leveriza Health Center (vaccinations: {clinicHoursShort})";
        string shortName = $"{abbreviation ?? vaccineName} {doseNumber}";
        int daysUntil = (scheduled.Date - today.Date).Days;

        if (daysUntil >= 1)
        {
            int threshold = PreReminders.Where(d => d >= daysUntil).DefaultIfEmpty(PreReminders.Max()).Min();
            string when = daysUntil == 1 ? "tomorrow" : $"in {daysUntil} days";
            return (PreDueType(threshold), $"Vaccine due {when} — {childName}",
                PreDueMessage(threshold, daysUntil, vaccineName, doseLabel, childName, dateLabel, clinicInfo),
                $"Leveriza Health Center: {childFirst}'s vaccine ({shortName}) is due {when}, {scheduled:ddd, MMM d}. Vaccinations: {clinicHoursShort}.");
        }
        if (daysUntil == 0)
        {
            return ("ReminderDay", $"Vaccine due today — {childName}",
                $"{vaccineName} ({doseLabel}) for {childName} is due today, {dateLabel}. Please visit {clinicInfo}.",
                $"Leveriza Health Center: {childFirst}'s vaccine ({shortName}) is due today, {scheduled:ddd, MMM d}. Vaccinations: {clinicHoursShort}.");
        }

        int daysLate = -daysUntil;
        int after = PostReminders.Where(d => d <= daysLate).DefaultIfEmpty(1).Max();
        var (title, message) = OverdueText(after, daysLate, vaccineName, doseLabel, childName, childFirst, dateLabel, clinicInfo);
        string sms = after == 1
            ? $"Leveriza Health Center: {childFirst} missed {shortName} on {scheduled:ddd, MMM d}. Please come on the next vaccination day ({clinicHoursShort})."
            : $"Leveriza Health Center: {childFirst}'s {shortName} is {daysLate} days overdue (due {scheduled:MMM d}). Please come on the next vaccination day ({clinicHoursShort}).";
        return (OverdueType(after), title, message, sms);
    }

    private static (string Title, string Message) OverdueText(
        int threshold, int daysLate, string vaccine, string dose,
        string childName, string firstName, string dateLabel, string clinicInfo)
    {
        string what = $"{vaccine} ({dose}) for {childName}";

        return threshold switch
        {
            1 => ($"Missed vaccine — {childName}",
                  $"{what} was scheduled on {dateLabel}. " +
                  $"Please visit {clinicInfo} as soon as possible."),
            5 => ($"Vaccine overdue — {childName}",
                  $"{what} is now {daysLate} days overdue (was due {dateLabel}). " +
                  $"Please visit {clinicInfo} this week to keep {firstName} protected."),
            14 => ($"2 weeks overdue — {childName}",
                  $"{what} is {daysLate} days overdue. " +
                  $"A delay this long may leave your child unprotected. Please visit {clinicInfo} urgently."),
            30 => ($"URGENT: 1 month overdue — {childName}",
                  $"{what} is now {daysLate} days overdue. " +
                  $"Immediate vaccination is strongly recommended. Please visit {clinicInfo}."),
            _ => ($"Vaccine overdue — {childName}",
                  $"{what} is overdue.")
        };
    }
}
