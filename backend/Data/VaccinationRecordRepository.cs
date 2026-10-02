using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.DTOs;
namespace AndroidWebAPI.Data.Repositories
{
    public class VaccinationRecordRepository : IVaccinationRecordRepository
    {
        private readonly AppDbContext _context;

        public VaccinationRecordRepository(AppDbContext context)
        {
            _context = context;
        }


        public async Task<IEnumerable<VaccinationRecord>> GetAllAsync()
        {
            return await _context.VaccinationRecords
                .Include(r => r.Child)
                    .ThenInclude(c => c.ParentRelationships)
                    .ThenInclude(pr => pr.Parent)
                .Include(r => r.Vaccine)
                .Include(r => r.Inventory)
                .Include(r => r.AdministeredBy)
                .ToListAsync();
        }
     


        public async Task<VaccinationRecord?> GetByIdAsync(Guid vaccinationRecordId)
        {
            return await _context.VaccinationRecords
                .FirstOrDefaultAsync(r => r.VaccinationRecordID == vaccinationRecordId);
        }
        
public async Task<IEnumerable<VaccinationRecordResponseDto>> GetByChildAsync(Guid childId)
{
    return await _context.VaccinationRecords
        .Where(r => r.ChildID == childId)
        .OrderBy(r => r.VaccinationDate)
        .Select(r => new VaccinationRecordResponseDto
        {
            VaccinationRecordID = r.VaccinationRecordID,
            RecordCode = r.RecordCode,
            ChildID = r.ChildID,
            VaccineID = r.VaccineID,
            VaccineName = r.Vaccine != null
                ? r.Vaccine.VaccineName
                : string.Empty,
            DoseNumber = r.DoseNumber,
            VaccinationDate = r.VaccinationDate,
            Status = r.Status,
            AdministeredByUserID = r.AdministeredByUserID,
            AdministeredByName = r.AdministeredBy != null
                ? r.AdministeredBy.FirstName + " " + r.AdministeredBy.LastName
                : null,
            NurseObservation = r.NurseObservation,
            InjectionSite = r.InjectionSite,
            LotNumber = r.Inventory != null ? r.Inventory.LotNumber : null
        })
        .ToListAsync();
}
        

public async Task<VaccinationRecord> CompleteVaccinationAsync(Guid vaccinationRecordId, CompleteVaccinationDto dto)
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        var record = await _context.VaccinationRecords
            .FirstOrDefaultAsync(r => r.VaccinationRecordID == vaccinationRecordId);

        if (record == null)
            throw new Exception("Vaccination record not found.");

        if (record.Status == "Completed")
            throw new Exception("This vaccination has already been completed.");

        var inventory = await _context.VaccineInventory
            .FirstOrDefaultAsync(i => i.InventoryID == dto.InventoryID);

        if (inventory == null)
            throw new Exception("Vaccine inventory not found.");

        if (inventory.VaccineID != record.VaccineID)
            throw new Exception("The selected inventory does not belong to this record's vaccine.");

        if (!inventory.Status)
            throw new Exception("The selected vaccine inventory is inactive.");

        if (inventory.ExpirationDate.Date < DateTime.Today)
            throw new Exception("The selected vaccine inventory has expired.");

        if (inventory.CurrentQuantity <= 0)
            throw new Exception("No vaccine stock remaining for the selected batch.");

        DeductStock(inventory);

        record.InventoryID = dto.InventoryID;
        record.AdministeredByUserID = dto.AdministeredByUserID;
        record.VaccinationDate = dto.VaccinationDate;
        record.NurseObservation = dto.NurseObservation;
        record.Status = "Completed";
        record.UpdatedAt = DateTime.UtcNow;

        await LinkTimelineAsync(record);

        await _context.SaveChangesAsync();

        await RecalculateFollowingDosesAsync(record.ChildID, record.VaccineID, record.DoseNumber, record.VaccinationDate);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        return record;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

        public async Task AddAsync(VaccinationRecord record)
        {
            await _context.VaccinationRecords.AddAsync(record);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(VaccinationRecord record)
        {
            _context.VaccinationRecords.Update(record);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid vaccinationRecordId)
        {
            var record = await _context.VaccinationRecords.FindAsync(vaccinationRecordId);

            if (record != null)
            {
                _context.VaccinationRecords.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> AlreadyVaccinatedAsync(Guid childId, int vaccineId, int doseNumber)
        {
            // Only a row that was actually administered counts as "already
            // vaccinated" — a Scheduled/Overdue row for the same dose (e.g.
            // a duplicate timeline entry) must not block recording the dose.
            return await _context.VaccinationRecords.AnyAsync(r =>
                r.ChildID == childId &&
                r.VaccineID == vaccineId &&
                r.DoseNumber == doseNumber &&
                r.Status == "Completed");
        }
        
public async Task RecordVaccinationAsync(VaccinationRecord record)
{
    await using var transaction =
        await _context.Database.BeginTransactionAsync();

    try
    {
        // ==========================================
        // 1. Prevent duplicate vaccination
        // ==========================================

        if (await AlreadyVaccinatedAsync(
            record.ChildID,
            record.VaccineID,
            record.DoseNumber))
        {
            throw new Exception(
                "This vaccine dose has already been recorded for this child.");
        }

        // ==========================================
        // 2. Verify child exists
        // ==========================================

        var childExists = await _context.Children
            .AnyAsync(c => c.ChildID == record.ChildID);

        if (!childExists)
            throw new Exception("Child not found.");

        // ==========================================
        // 3. Verify vaccine exists
        // ==========================================

        var vaccine = await _context.Vaccines
            .FirstOrDefaultAsync(v => v.VaccineID == record.VaccineID);

        if (vaccine == null)
            throw new Exception("Vaccine not found.");

        // ==========================================
        // 3b. Doses go in order (Penta 3 can't be
        //     given before Penta 2), never ahead
        // ==========================================

        if (record.DoseNumber < 1 ||
            (vaccine.NumberOfRequiredDoses > 0 && record.DoseNumber > vaccine.NumberOfRequiredDoses))
        {
            throw new Exception(
                $"{vaccine.VaccineName} has {vaccine.NumberOfRequiredDoses} dose(s); there is no Dose {record.DoseNumber}.");
        }

        if (record.DoseNumber > 1 &&
            !await AlreadyVaccinatedAsync(record.ChildID, record.VaccineID, record.DoseNumber - 1))
        {
            throw new Exception(
                $"Dose {record.DoseNumber - 1} of {vaccine.VaccineName} hasn't been recorded yet. " +
                "If it was given elsewhere, add it from the child's Yellow Book (Patient Records) first.");
        }

        if (record.VaccinationDate.Date > DateTime.Today)
            throw new Exception("The vaccination date can't be in the future.");

        // ==========================================
        // 4. Inventory is REQUIRED for clinic
        // ==========================================

        if (!record.InventoryID.HasValue)
        {
            throw new Exception(
                "InventoryID is required when recording a clinic vaccination.");
        }

        // ==========================================
        // 5. Find inventory
        // ==========================================

        var inventory = await _context.VaccineInventory
            .FirstOrDefaultAsync(i =>
                i.InventoryID == record.InventoryID.Value);

        if (inventory == null)
            throw new Exception("Vaccine inventory not found.");

        // ==========================================
        // 6. Make sure inventory belongs to vaccine
        // ==========================================

        if (inventory.VaccineID != record.VaccineID)
        {
            throw new Exception(
                "The selected inventory does not belong to the selected vaccine.");
        }

        // ==========================================
        // 7. Check inventory status
        // ==========================================

        if (!inventory.Status)
        {
            throw new Exception(
                "The selected vaccine inventory is inactive.");
        }

        // ==========================================
        // 8. Check expiration
        // ==========================================

        if (inventory.ExpirationDate.Date < DateTime.Today)
        {
            throw new Exception(
                "The selected vaccine inventory has expired.");
        }

        // ==========================================
        // 9. Check stock
        // ==========================================

        if (inventory.CurrentQuantity <= 0)
        {
            throw new Exception(
                "No vaccine stock remaining.");
        }

        // ==========================================
        // 10. Deduct inventory (+ low-stock alert)
        // ==========================================

        DeductStock(inventory);

        // ==========================================
        // 11. Create vaccination record
        // ==========================================

        record.VaccinationRecordID = Guid.NewGuid();

        record.RecordCode =
            $"VR-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";

        record.Status = "Completed";

        record.CreatedAt = DateTime.UtcNow;

        // ==========================================
        // 12-13. Complete and link the matching
        //        timeline entry (Pending or Missed)
        // ==========================================

        await LinkTimelineAsync(record);

        // ==========================================
        // 14. Save everything
        // ==========================================

        await _context.VaccinationRecords.AddAsync(record);

        await _context.SaveChangesAsync();

        // ==========================================
        // 14b. Recalculation engine — push this
        //      vaccine's later doses out from the
        //      ACTUAL administration date
        // ==========================================

        await RecalculateFollowingDosesAsync(record.ChildID, record.VaccineID, record.DoseNumber, record.VaccinationDate);

        await _context.SaveChangesAsync();

        // ==========================================
        // 15. Commit transaction
        // ==========================================

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

public async Task RecordHistoricalVaccinationsAsync(
    HistoricalVaccinationSubmissionDto submission)
{
    await using var transaction =
        await _context.Database.BeginTransactionAsync();

    try
    {
        // 1. Verify child exists
        var child = await _context.Children
            .FirstOrDefaultAsync(c => c.ChildID == submission.ChildID);

        if (child == null)
            throw new Exception("Child not found.");

        // 2. Nothing to save
        if (submission.Vaccinations == null ||
    !submission.Vaccinations.Any())
        {
            throw new Exception(
                "No historical vaccination records were provided.");
        }

        // 3. Process each historical vaccination
        foreach (var item in submission.Vaccinations)
        {
            // Verify vaccine exists
            var vaccineExists = await _context.Vaccines
                .AnyAsync(v => v.VaccineID == item.VaccineID);

            if (!vaccineExists)
                throw new Exception(
                    $"Vaccine {item.VaccineID} not found.");

            // A Yellow Book date lies between the birth date and today
            if (item.VaccinationDate.Date > DateTime.Today || item.VaccinationDate.Date < child.BirthDate.Date)
                throw new Exception(
                    $"The date for vaccine {item.VaccineID} dose {item.DoseNumber} must be between the birth date and today.");

            // Prevent duplicate vaccine + dose for this child
            var alreadyExists = await AlreadyVaccinatedAsync(
                submission.ChildID,
                item.VaccineID,
                item.DoseNumber);

            if (alreadyExists)
                throw new Exception(
                    $"Dose {item.DoseNumber} for vaccine {item.VaccineID} "
                    + "has already been recorded.");

            // Create historical record
            var record = new VaccinationRecord
            {
                VaccinationRecordID = Guid.NewGuid(),

                ChildID = submission.ChildID,

                VaccineID = item.VaccineID,
                RecordCode = $"HIST-{Guid.NewGuid().ToString("N")[..20]}".ToUpper(),

                // Historical record:
                // no clinic inventory was used.
                InventoryID = null,

                // Not linked to a timeline yet.
                TimelineID = null,

                DoseNumber = item.DoseNumber,

                VaccinationDate = item.VaccinationDate,

                Status = "Completed",

                CreatedAt = DateTime.UtcNow
            };

            // Tick off the matching timeline entry so the child's schedule
            // doesn't keep showing an already-given dose as due/missed.
            await LinkTimelineAsync(record);

            await _context.VaccinationRecords.AddAsync(record);
        }

        // 4. Save all records together
        await _context.SaveChangesAsync();

        // 4b. Re-plan the remaining doses of each vaccine from its most
        //     recent historical dose.
        var latestPerVaccine = submission.Vaccinations
            .GroupBy(v => v.VaccineID)
            .Select(g => g.OrderByDescending(v => v.DoseNumber).First());

        foreach (var latest in latestPerVaccine)
        {
            await RecalculateFollowingDosesAsync(
                submission.ChildID, latest.VaccineID, latest.DoseNumber, latest.VaccinationDate);
        }

        await _context.SaveChangesAsync();

        // 5. Commit transaction
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

        // =====================================================
        // SHARED HELPERS
        // =====================================================

        // DOH minimum spacing between two doses of the same vaccine.
        private const int MinimumDoseIntervalDays = 28;

        // Marks the child's timeline entry for this vaccine/dose as done.
        // Matches Missed as well as Pending — a child who comes in late for
        // a dose that already rolled over to Missed still gets it ticked off.
        private async Task LinkTimelineAsync(VaccinationRecord record)
        {
            var timeline = await _context.VaccinationTimelines
                .FirstOrDefaultAsync(t =>
                    t.ChildID == record.ChildID &&
                    t.VaccineID == record.VaccineID &&
                    t.DoseNumber == record.DoseNumber &&
                    (t.Status == "Pending" || t.Status == "Missed"));

            if (timeline == null) return;

            timeline.Status = "Completed";
            timeline.CompletedDate = record.VaccinationDate.Date;
            timeline.VaccinationRecordID = record.VaccinationRecordID;
            timeline.UpdatedAt = DateTime.UtcNow;
            record.TimelineID = timeline.TimelineID;
        }

        // RECALCULATION ENGINE
        // After dose N of a vaccine is given on `administeredOn`, every later
        // dose of that vaccine that hasn't been given yet is re-planned from
        // the ACTUAL date instead of the original plan:
        //
        //   next due = later of
        //     • the age-based date (birth + RecommendedAgeDays) — child on schedule
        //     • previous dose + max(rule interval, 28 days)  — child is late
        //
        // then moved forward to the next day the clinic is open. Each later
        // dose cascades from the one before it.
        private async Task RecalculateFollowingDosesAsync(
            Guid childId, int vaccineId, int doseNumber, DateTime administeredOn)
        {
            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildID == childId);
            if (child == null) return;

            var following = await _context.VaccinationTimelines
                .Where(t =>
                    t.ChildID == childId &&
                    t.VaccineID == vaccineId &&
                    t.DoseNumber > doseNumber &&
                    (t.Status == "Pending" || t.Status == "Missed"))
                .OrderBy(t => t.DoseNumber)
                .ToListAsync();

            if (following.Count == 0) return;

            var rules = await _context.VaccinationScheduleRules
                .Where(r => r.VaccineID == vaccineId)
                .ToListAsync();

            var previous = administeredOn.Date;

            foreach (var timeline in following)
            {
                var rule = rules.FirstOrDefault(r => r.DoseNumber == timeline.DoseNumber);

                var ageBased = child.BirthDate.Date.AddDays(rule?.RecommendedAgeDays ?? 0);
                var interval = Math.Max(rule?.IntervalFromPreviousDoseDays ?? 0, MinimumDoseIntervalDays);
                var fromPrevious = previous.AddDays(interval);

                var due = ageBased > fromPrevious ? ageBased : fromPrevious;
                var scheduled = await AndroidWebAPI.Services.ClinicCalendar.NextOpenDayAsync(_context, due);

                if (timeline.ScheduledDate.Date != scheduled || timeline.Status == "Missed")
                {
                    timeline.ScheduledDate = scheduled;
                    timeline.Status = scheduled < DateTime.Today ? "Missed" : "Pending";
                    timeline.UpdatedAt = DateTime.UtcNow;
                }

                previous = scheduled;
            }
        }

        // Takes one dose out of a batch. When that pushes the batch below its
        // minimum stock level, the Admin / Doctor gets a bell notification and
        // decides what to order; "Alert Staff" on the Admin Inventory page then
        // tells the Staff / Nurses. The full weekly picture goes to everyone
        // through StockCheck (every Wednesday).
        private void DeductStock(VaccineInventory inventory)
        {
            int before = inventory.CurrentQuantity;
            inventory.CurrentQuantity--;
            inventory.UpdatedAt = DateTime.UtcNow;

            if (before < inventory.MinimumStock || inventory.CurrentQuantity >= inventory.MinimumStock)
                return;

            var vaccineName = _context.Vaccines
                .Where(v => v.VaccineID == inventory.VaccineID)
                .Select(v => v.VaccineName)
                .FirstOrDefault() ?? $"Vaccine {inventory.VaccineID}";

            var recipients = _context.Users
                .Where(u => u.AccountStatus == "Active" &&
                            (u.Position == "Doctor" || u.Position == "Administrator"))
                .Select(u => u.UserID)
                .ToList();

            foreach (var userId in recipients)
            {
                _context.Notifications.Add(new Notification
                {
                    NotificationID = Guid.NewGuid(),
                    UserID = userId,
                    VaccineID = inventory.VaccineID,
                    Type = "LowStock",
                    Title = $"Low stock — {vaccineName}",
                    Message = $"Batch {inventory.LotNumber} of {vaccineName} is down to {inventory.CurrentQuantity} dose(s), " +
                              $"below the minimum of {inventory.MinimumStock}. Use \"Alert Staff\" on the Inventory page to have the staff request more.",
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                });
            }
        }
    }
}