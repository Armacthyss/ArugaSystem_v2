using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Data
{
    public class VaccinationTimelineRepository : IVaccinationTimelineRepository
    {
        private readonly AppDbContext _context;

        public VaccinationTimelineRepository(AppDbContext context)
        {
            _context = context;
        }

        // ============================
        // CRUD
        // ============================

        public async Task<IEnumerable<VaccinationTimeline>> GetAllAsync()
        {
            return await _context.VaccinationTimelines
                .Include(t => t.Child)
                .Include(t => t.Vaccine)
                .ToListAsync();
        }

        public async Task<VaccinationTimeline?> GetByIdAsync(Guid timelineId)
        {
            return await _context.VaccinationTimelines
                .Include(t => t.Child)
                .Include(t => t.Vaccine)
                .FirstOrDefaultAsync(t => t.TimelineID == timelineId);
        }
public async Task<IEnumerable<VaccinationTimeline>> GetByChildAsync(Guid childId)
{
    return await _context.VaccinationTimelines
        .Where(t => t.ChildID == childId)
        .Include(t => t.Child)
        .Include(t => t.Vaccine)
        .OrderBy(t => t.ExpectedDate)
        .ToListAsync();
}
        public async Task AddAsync(VaccinationTimeline timeline)
        {
            await _context.VaccinationTimelines.AddAsync(timeline);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(VaccinationTimeline timeline)
        {
            _context.VaccinationTimelines.Update(timeline);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid timelineId)
        {
            var timeline = await _context.VaccinationTimelines.FindAsync(timelineId);

            if (timeline != null)
            {
                _context.VaccinationTimelines.Remove(timeline);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(Guid childId, int vaccineId, int doseNumber)
        {
            return await _context.VaccinationTimelines.AnyAsync(t =>
                t.ChildID == childId &&
                t.VaccineID == vaccineId &&
                t.DoseNumber == doseNumber);
        }

        // ============================
        // BUSINESS LOGIC
        // ============================
        // 
        public async Task GenerateTimelineAsync(Guid childId)
{
    // 1. Get child
    var child = await _context.Children
        .FirstOrDefaultAsync(c => c.ChildID == childId);

    if (child == null)
        throw new Exception("Child not found.");

    // 2. Prevent duplicate timeline generation
    bool timelineExists = await _context.VaccinationTimelines
        .AnyAsync(t =>
            t.ChildID == childId &&
            t.Status == "Pending");

    if (timelineExists)
        throw new Exception(
            "Vaccination timeline already exists for this child.");

    // 3. Load vaccination schedule rules
    var rules = await _context.VaccinationScheduleRules
        .OrderBy(r => r.SequenceOrder)
        .ToListAsync();

    if (!rules.Any())
        throw new Exception(
            "No vaccination schedule rules found.");

    // 4. Load existing vaccination records
    var vaccinationRecords = await _context.VaccinationRecords
        .Where(r => r.ChildID == childId)
        .ToListAsync();

    // 5. Load existing timelines
    var existingTimelines = await _context.VaccinationTimelines
        .Where(t => t.ChildID == childId)
        .ToListAsync();

    // 6. Generate only missing timelines
    var timelines = new List<VaccinationTimeline>();

    foreach (var rule in rules)
    {
        var existingTimeline = existingTimelines
            .FirstOrDefault(t =>
                t.VaccineID == rule.VaccineID &&
                t.DoseNumber == rule.DoseNumber);

        if (existingTimeline != null)
            continue;

        var existingRecord = vaccinationRecords
            .FirstOrDefault(r =>
                r.VaccineID == rule.VaccineID &&
                r.DoseNumber == rule.DoseNumber &&
                r.Status == "Completed");

        var expectedDate =
            child.BirthDate.AddDays(rule.RecommendedAgeDays);

        var scheduledDate = await GetNextAvailableVaccinationDateAsync(expectedDate);

var timeline = new VaccinationTimeline
{
    TimelineID = Guid.NewGuid(),

    TimelineCode =
        $"TL-{Guid.NewGuid().ToString("N")[..12].ToUpper()}",

    ChildID = child.ChildID,
    VaccineID = rule.VaccineID,
    DoseNumber = rule.DoseNumber,

    ExpectedDate = expectedDate,
    ScheduledDate = scheduledDate,

    CreatedAt = DateTime.UtcNow
};

        if (existingRecord != null)
        {
            timeline.Status = "Completed";
            timeline.CompletedDate = existingRecord.VaccinationDate.Date;
            timeline.VaccinationRecordID =
                existingRecord.VaccinationRecordID;
        }
        else
        {
            timeline.Status = "Pending";
            timeline.VaccinationRecordID = null;
        }

        timelines.Add(timeline);
    }

    // 7. Save
    if (timelines.Any())
    {
        await _context.VaccinationTimelines
            .AddRangeAsync(timelines);

        await _context.SaveChangesAsync();
    }
}

private async Task<DateTime> GetNextAvailableVaccinationDateAsync(
    DateTime expectedDate)
{
    var date = expectedDate.Date;

    // Safety limit so we don't accidentally loop forever
    for (int i = 0; i < 365; i++)
    {
        // 1. Check if there is a special exception for this date
        var exception = await _context.ClinicScheduleExceptions
            .FirstOrDefaultAsync(e =>
                e.ExceptionDate.Date == date &&
                e.IsActive);

        if (exception != null)
        {
            // Exception overrides the normal weekly schedule
            if (exception.IsOpen)
            {
                return date;
            }

            // Exception says clinic is closed
            date = date.AddDays(1);
            continue;
        }

        // 2. No exception, so check normal weekly schedule
        int dayOfWeek = (int)date.DayOfWeek;

        var schedule = await _context.ClinicOperatingSchedules
            .FirstOrDefaultAsync(s =>
                s.DayOfWeek == dayOfWeek &&
                s.IsActive);

        // 3. Normal schedule says clinic is open
        if (schedule != null && schedule.IsOpen)
        {
            return date;
        }

        // 4. Closed normally → try the next day
        date = date.AddDays(1);
    }

    throw new Exception(
        "No available pediatric vaccination schedule found within the next 365 days.");
}
                public async Task MarkCompletedAsync(Guid timelineId)
{
    var timeline = await _context.VaccinationTimelines
        .FirstOrDefaultAsync(t => t.TimelineID == timelineId);

    if (timeline == null)
        throw new Exception("Vaccination timeline not found.");

    timeline.Status = "Completed";
    timeline.UpdatedAt = DateTime.Now;

    _context.VaccinationTimelines.Update(timeline);
    await _context.SaveChangesAsync();
}
public async Task<IEnumerable<VaccinationTimeline>> GetDueTodayAsync()
{
    var today = DateTime.Today;

    return await _context.VaccinationTimelines
        .Include(t => t.Child)
            .ThenInclude(c => c.ParentRelationships)
            .ThenInclude(pr => pr.Parent)
        .Include(t => t.Vaccine)
        .Where(t =>
            t.Status == "Pending" &&
            t.ScheduledDate.Date == today)
        .OrderBy(t => t.ScheduledDate)
        .ToListAsync();
}

public async Task<IEnumerable<VaccinationTimeline>> GetUpcomingAsync(int days)
{
    var today = DateTime.Today;
    var endDate = today.AddDays(days);

    return await _context.VaccinationTimelines
        .Include(t => t.Child)
        .Include(t => t.Vaccine)
        .Where(t =>
            t.Status == "Pending" &&
            t.ScheduledDate.Date >= today &&
            t.ScheduledDate.Date <= endDate)
        .OrderBy(t => t.ScheduledDate)
        .ToListAsync();
}

// Recomputes every dose that hasn't been given yet, the same way the
// recalculation engine does after a vaccination: a dose is due at the
// recommended age, but never sooner than 28 days (or the rule's interval)
// after the previous dose of the same vaccine, then moved to the next open
// clinic day. Used when a child's birth date is corrected.
public async Task RescheduleChildAsync(Guid childId)
{
    const int MinimumDoseIntervalDays = 28;

    var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildID == childId);
    if (child == null) return;

    var timelines = await _context.VaccinationTimelines
        .Where(t => t.ChildID == childId && t.Status != "Cancelled")
        .ToListAsync();
    var rules = await _context.VaccinationScheduleRules.ToListAsync();
    var records = await _context.VaccinationRecords
        .Where(r => r.ChildID == childId && r.Status == "Completed")
        .ToListAsync();

    foreach (var vaccine in timelines.GroupBy(t => t.VaccineID))
    {
        DateTime? previous = null;

        foreach (var timeline in vaccine.OrderBy(t => t.DoseNumber))
        {
            var rule = rules.FirstOrDefault(r => r.VaccineID == timeline.VaccineID && r.DoseNumber == timeline.DoseNumber);
            var ageBased = child.BirthDate.Date.AddDays(rule?.RecommendedAgeDays ?? 0);

            if (timeline.Status == "Completed")
            {
                timeline.ExpectedDate = ageBased;
                previous = timeline.CompletedDate
                    ?? records.FirstOrDefault(r => r.VaccineID == timeline.VaccineID && r.DoseNumber == timeline.DoseNumber)?.VaccinationDate.Date
                    ?? timeline.ScheduledDate;
                continue;
            }

            var due = ageBased;
            if (previous != null)
            {
                var interval = Math.Max(rule?.IntervalFromPreviousDoseDays ?? 0, MinimumDoseIntervalDays);
                var fromPrevious = previous.Value.AddDays(interval);
                if (fromPrevious > due) due = fromPrevious;
            }

            var scheduled = await AndroidWebAPI.Services.ClinicCalendar.NextOpenDayAsync(_context, due);

            timeline.ExpectedDate = ageBased;
            timeline.ScheduledDate = scheduled;
            timeline.Status = scheduled < DateTime.Today ? "Missed" : "Pending";
            timeline.UpdatedAt = DateTime.UtcNow;

            previous = scheduled;
        }
    }

    await _context.SaveChangesAsync();
}

// Children registered in the app get their schedule right away; children
// added straight into the database don't, so their Schedule and Records
// pages would only list doses already given. This builds the missing
// schedule the same way (doses already given count as done, the 28-day
// rule applies to the rest, past due dates become overdue).
public async Task<bool> EnsureTimelineAsync(Guid childId)
{
    if (await _context.VaccinationTimelines.AnyAsync(t => t.ChildID == childId)) return false;
    if (!await _context.Children.AnyAsync(c => c.ChildID == childId)) return false;

    await GenerateTimelineAsync(childId);
    await RescheduleChildAsync(childId);
    return true;
}

public async Task<int> EnsureAllTimelinesAsync()
{
    var missing = await _context.Children
        .Where(c => !_context.VaccinationTimelines.Any(t => t.ChildID == c.ChildID))
        .Select(c => c.ChildID)
        .ToListAsync();

    foreach (var childId in missing)
        await EnsureTimelineAsync(childId);

    return missing.Count;
}

// A dose that was given (e.g. entered from the Yellow Book before historical
// entries were linked to the schedule) is marked as given on the schedule,
// and that child's remaining doses are recalculated from it.
public async Task<int> LinkGivenDosesAsync()
{
    var unlinked = await (
        from t in _context.VaccinationTimelines
        where t.Status != "Completed" && t.Status != "Cancelled"
        join r in _context.VaccinationRecords
            on new { t.ChildID, t.VaccineID, t.DoseNumber } equals new { r.ChildID, r.VaccineID, r.DoseNumber }
        where r.Status == "Completed"
        select new { Timeline = t, Record = r }).ToListAsync();

    foreach (var pair in unlinked)
    {
        pair.Timeline.Status = "Completed";
        pair.Timeline.CompletedDate = pair.Record.VaccinationDate.Date;
        pair.Timeline.VaccinationRecordID = pair.Record.VaccinationRecordID;
        pair.Timeline.UpdatedAt = DateTime.UtcNow;
        pair.Record.TimelineID ??= pair.Timeline.TimelineID;
    }
    await _context.SaveChangesAsync();

    var children = unlinked.Select(p => p.Timeline.ChildID).Distinct().ToList();
    foreach (var childId in children)
        await RescheduleChildAsync(childId);

    return children.Count;
}

// When the admin closes a day (weekly hours or a holiday), doses still to
// come on that day are moved: the child's schedule is recalculated so every
// dose lands on an open day and keeps the 28-day spacing.
public async Task<int> MoveDosesOffClosedDaysAsync()
{
    var isOpen = await AndroidWebAPI.Services.ClinicCalendar.OpenDayCheckAsync(_context);
    var today = DateTime.Today;

    var upcoming = await _context.VaccinationTimelines
        .Where(t => t.Status == "Pending" && t.ScheduledDate >= today)
        .Select(t => new { t.ChildID, t.ScheduledDate })
        .ToListAsync();

    var children = upcoming
        .Where(t => !isOpen(t.ScheduledDate))
        .Select(t => t.ChildID)
        .Distinct()
        .ToList();

    foreach (var childId in children)
        await RescheduleChildAsync(childId);

    return children.Count;
}

public async Task RegenerateTimelineAsync(Guid childId)
{
    // Get the child
    var child = await _context.Children
        .FirstOrDefaultAsync(c => c.ChildID == childId);

    if (child == null)
        throw new Exception("Child not found.");

    // Delete only pending timeline records
    var pendingTimelines = await _context.VaccinationTimelines
        .Where(t => t.ChildID == childId &&
                    t.Status == "Pending")
        .ToListAsync();

    _context.VaccinationTimelines.RemoveRange(pendingTimelines);
    await _context.SaveChangesAsync();

    // Generate a new timeline
    await GenerateTimelineAsync(childId);
}
public async Task<TimelineSummaryDto> GetTimelineSummaryAsync(Guid childId)
{
    var timelines = await _context.VaccinationTimelines
        .Where(t => t.ChildID == childId)
        .ToListAsync();

    return new TimelineSummaryDto
    {
        ChildID = childId,
        HasTimeline = timelines.Any(),
        Pending = timelines.Count(t => t.Status == "Pending"),
        Completed = timelines.Count(t => t.Status == "Completed"),
        Missed = timelines.Count(t => t.Status == "Missed")
    };
}

public async Task UpdateMissedVaccinationsAsync()
{
    var today = DateTime.Today;

    var overdueTimelines = await _context.VaccinationTimelines
        .Where(t =>
            t.Status == "Pending" &&
            t.ScheduledDate.Date < today)
        .ToListAsync();

    foreach (var timeline in overdueTimelines)
    {
        timeline.Status = "Missed";
        timeline.UpdatedAt = DateTime.UtcNow;
    }

    if (overdueTimelines.Any())
    {
        await _context.SaveChangesAsync();
    }
}
    }
}