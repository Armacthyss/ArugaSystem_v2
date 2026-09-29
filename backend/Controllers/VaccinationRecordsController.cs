using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Services;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VaccinationRecordsController : ControllerBase
    {
        private readonly IVaccinationRecordRepository _repository;
        private readonly AppDbContext _context;
        private readonly ILogger<VaccinationRecordsController> _logger;
        private readonly AuditService _audit;

        public VaccinationRecordsController(
            IVaccinationRecordRepository repository,
            AppDbContext context,
            ILogger<VaccinationRecordsController> logger,
            AuditService audit)
        {
            _repository = repository;
            _context = context;
            _logger = logger;
            _audit = audit;
        }

        // "Juan Dela Cruz – BCG Dose 1" label used in audit entries.
        private async Task<string> DescribeDoseAsync(Guid childId, int vaccineId, int doseNumber)
        {
            var child = await _context.Children.FindAsync(childId);
            var vaccine = await _context.Vaccines.FindAsync(vaccineId);
            var childName = child != null ? $"{child.FirstName} {child.LastName}" : "Unknown child";
            return $"{childName} – {vaccine?.VaccineName ?? $"Vaccine {vaccineId}"} Dose {doseNumber}";
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _repository.GetAllAsync());
        }

        // Explicit "/all" route — the frontend (Doctor Vaccination Records,
        // Doctor Calendar, Doctor Reports) calls GET /api/VaccinationRecords/all.
        // Without this, "all" fell through to GetById(Guid id) below and
        // failed model binding ("The value 'all' is not valid.", 400).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllExplicit()
        {
            var records = await _repository.GetAllAsync();
            return Ok(ShapeRecords(records));
        }

        // GET /api/VaccinationRecords/mine
        // "Vaccination Records" page: only the doses the signed-in Doctor or
        // Nurse gave. Health workers don't see each other's lists
        // (beneficiary request); a child's own card still shows every dose.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            var me = AccessGuard.CallerId(User);
            if (me == null) return Ok(Array.Empty<object>());
            var records = (await _repository.GetAllAsync()).Where(r => r.AdministeredByUserID == me);
            return Ok(ShapeRecords(records));
        }

        // GetAllAsync() returns raw VaccinationRecord entities, which serialize
        // with the bare PascalCase model properties (ChildID, VaccineID, etc.)
        // instead of the childName/vaccineName/administeredByName shape the
        // frontend expects (same shape GetByChildAsync's DTO already produces).
        // This projects those entities into that shape without touching
        // GetAllAsync() itself, since Stats/PendingToday/CompletedToday below
        // rely on it returning navigable entities, not a DTO.
        private static IEnumerable<object> ShapeRecords(IEnumerable<VaccinationRecord> records)
        {
            return records.Select(r => new
            {
                vaccinationRecordID = r.VaccinationRecordID,
                recordCode = r.RecordCode,
                childID = r.ChildID,
                childName = r.Child != null ? $"{r.Child.FirstName} {r.Child.LastName}".Trim() : "Unknown",
                parentName = GetPrimaryParentName(r.Child),
                vaccineID = r.VaccineID,
                vaccineName = r.Vaccine != null ? r.Vaccine.VaccineName : $"Vaccine {r.VaccineID}",
                doseNumber = r.DoseNumber,
                vaccinationDate = r.VaccinationDate,
                status = r.Status,
                administeredByUserID = r.AdministeredByUserID,
                administeredByName = r.AdministeredBy != null
                    ? $"{r.AdministeredBy.FirstName} {r.AdministeredBy.LastName}".Trim()
                    : null,
                nurseObservation = r.NurseObservation,
                lotNumber = r.Inventory != null ? r.Inventory.LotNumber : null,
            });
        }

        // Mirrors the primary-contact lookup in NotifyParentAsync below.
        private static string? GetPrimaryParentName(Child? child)
        {
            var primaryParent = child?.ParentRelationships?
                .FirstOrDefault(pr => pr.IsPrimaryContact && pr.Status == "Active")?
                .Parent;

            return primaryParent != null
                ? $"{primaryParent.FirstName} {primaryParent.LastName}".Trim()
                : null;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var record = await _repository.GetByIdAsync(id);
            if (record == null) return NotFound();
            return Ok(record);
        }

        [HttpGet("child/{childId}")]
        public async Task<IActionResult> GetByChild(Guid childId)
        {
            if (!await AndroidWebAPI.Services.AccessGuard.CanSeeChildAsync(User, _context, childId)) return Forbid();
            return Ok(await _repository.GetByChildAsync(childId));
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Staff)]
        [HttpPost]
        public async Task<IActionResult> RecordVaccination([FromBody] RecordVaccinationDto dto)
        {
            // The signed-in Nurse is the one giving the dose
            dto.AdministeredByUserID = AccessGuard.CallerId(User) ?? dto.AdministeredByUserID;

            var roomError = await CheckCalledInAsync(dto.ChildID, dto.VaccinationDate);
            if (roomError != null)
                return BadRequest(new { message = roomError });

            var record = new VaccinationRecord
            {
                ChildID = dto.ChildID,
                VaccineID = dto.VaccineID,
                DoseNumber = dto.DoseNumber,
                VaccinationDate = dto.VaccinationDate,
                InventoryID = dto.InventoryID,
                AdministeredByUserID = dto.AdministeredByUserID,
                NurseObservation = dto.NurseObservation,
            };

            try
            {
                await _repository.RecordVaccinationAsync(record);
                await NotifyParentAsync(record);

                await _audit.LogAsync("Vaccination", "Vaccinate Child",
                    await DescribeDoseAsync(record.ChildID, record.VaccineID, record.DoseNumber),
                    string.IsNullOrWhiteSpace(record.NurseObservation)
                        ? "Recorded an administered vaccine dose."
                        : $"Recorded an administered vaccine dose. Remarks: {record.NurseObservation}",
                    userId: record.AdministeredByUserID,
                    newValue: $"Record {record.RecordCode}");

                return Ok(new
                {
                    message = "Vaccination recorded successfully.",
                    vaccinationRecordID = record.VaccinationRecordID,
                    recordCode = record.RecordCode
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RecordVaccination failed for Child {ChildId}", dto.ChildID);
                return BadRequest(new { message = ex.Message });
            }
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Staff)]
        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteVaccination(Guid id, [FromBody] CompleteVaccinationDto dto)
        {
            try
            {
                var record = await _repository.CompleteVaccinationAsync(id, dto);
                await NotifyParentAsync(record);

                await _audit.LogAsync("Vaccination", "Vaccinate Child",
                    await DescribeDoseAsync(record.ChildID, record.VaccineID, record.DoseNumber),
                    "Completed a scheduled vaccination.",
                    userId: record.AdministeredByUserID);

                return Ok(new
                {
                    message = "Vaccination recorded successfully.",
                    vaccinationRecordID = record.VaccinationRecordID
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CompleteVaccination failed for Record {RecordId}", id);
                return BadRequest(new { message = ex.Message });
            }
        }

        // PATCH /api/VaccinationRecords/{id}/remarks
        // Adds or corrects the remarks on a dose that was already given —
        // e.g. the parent reports a fever or swelling a day later. Remarks
        // (Vaccinationrecords.NurseObservation) are what health workers
        // check for complications/adverse reactions before the next dose.
        // Every change is kept in the audit log (old -> new).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPatch("{id}/remarks")]
        public async Task<IActionResult> UpdateRemarks(Guid id, [FromBody] UpdateRemarksDto dto)
        {
            var record = await _context.VaccinationRecords.FirstOrDefaultAsync(r => r.VaccinationRecordID == id);
            if (record == null)
                return NotFound(new { message = "Vaccination record not found." });

            var before = record.NurseObservation;
            record.NurseObservation = string.IsNullOrWhiteSpace(dto.Remarks) ? null : dto.Remarks.Trim();
            record.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Vaccination", "Update Remarks",
                await DescribeDoseAsync(record.ChildID, record.VaccineID, record.DoseNumber),
                "Updated the remarks / adverse reaction notes on a vaccination record.",
                userId: AccessGuard.CallerId(User) ?? dto.UpdatedByUserID,
                oldValue: before ?? "—",
                newValue: record.NurseObservation ?? "—");

            return Ok(new
            {
                message = "Remarks updated.",
                vaccinationRecordID = record.VaccinationRecordID,
                remarks = record.NurseObservation,
                updatedAt = record.UpdatedAt,
            });
        }

        // Today's doses can only be recorded for a child whose family was
        // called into the vaccination room (Call Next). Doses dated in the
        // past are allowed (encoding a dose that wasn't recorded on the
        // day), so this only applies to today's date.
        private async Task<string?> CheckCalledInAsync(Guid childId, DateTime vaccinationDate)
        {
            var today = DateTime.Today;
            if (vaccinationDate.Date != today) return null;

            bool inRoom = await _context.Queues
                .AnyAsync(q => q.QueueDate >= today && q.QueueDate < today.AddDays(1)
                            && q.Status == "InProgress"
                            && q.QueueChildren.Any(qc => qc.ChildID == childId));

            return inRoom
                ? null
                : "This child hasn't been called into the vaccination room yet. Press Call Next on the Dashboard first.";
        }

        // Tells the child's parents (in-app + email) that a dose was given, and
        // when the next one is due. Logs (instead of silently returning) when
        // it can't, so this never fails invisibly again.
        private async Task NotifyParentAsync(VaccinationRecord record)
        {
            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildID == record.ChildID);

            if (child == null)
            {
                _logger.LogWarning("Vaccination {RecordId} completed but Child {ChildId} not found — no notification sent.",
                    record.VaccinationRecordID, record.ChildID);
                return;
            }

            var notifier = HttpContext.RequestServices.GetRequiredService<ParentNotifier>();
            var parents = await notifier.ParentsOfChildAsync(child.ChildID);

            if (parents.Count == 0)
            {
                _logger.LogWarning("Vaccination {RecordId} completed for Child {ChildId} but no active parent link found — no notification sent.",
                    record.VaccinationRecordID, record.ChildID);
                return;
            }

            var vaccine = await _context.Vaccines.FindAsync(record.VaccineID);
            string childName = $"{child.FirstName} {child.LastName}";
            string vaccineName = vaccine?.VaccineName ?? $"Vaccine {record.VaccineID}";
            string dateLabel = record.VaccinationDate.ToString("MMMM d, yyyy");

            // Next dose still to come, after the recalculation engine has run
            var next = await _context.VaccinationTimelines
                .Include(t => t.Vaccine)
                .Where(t => t.ChildID == child.ChildID && (t.Status == "Pending" || t.Status == "Missed"))
                .OrderBy(t => t.ScheduledDate)
                .FirstOrDefaultAsync();
            string nextLine = next == null
                ? $" {child.FirstName} has no more scheduled doses on file."
                : $" Next: {next.Vaccine?.VaccineName ?? "vaccine"} (Dose {next.DoseNumber}) on {next.ScheduledDate:MMMM d, yyyy}.";

            var tally = new ParentNotifier.Delivery();
            foreach (var parent in parents)
            {
                await notifier.NotifyAsync(parent, new Notification
                {
                    ChildID = record.ChildID,
                    VaccineID = record.VaccineID,
                    DoseNumber = record.DoseNumber,
                    Type = "Completed",
                    Title = $"Vaccine administered — {childName}",
                    Message = $"{vaccineName} (Dose {record.DoseNumber}) was administered to {childName} on {dateLabel}.{nextLine}",
                    ScheduledDate = record.VaccinationDate,
                    IsRead = false,
                }, tally, sms: false);
            }

            await _context.SaveChangesAsync();
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut]
        public async Task<IActionResult> Update(VaccinationRecord record)
        {
            await _repository.UpdateAsync(record);
            return NoContent();
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpPost("historical")]
        public async Task<IActionResult> RecordHistoricalVaccinations([FromBody] HistoricalVaccinationSubmissionDto submission)
        {
            try
            {
                await _repository.RecordHistoricalVaccinationsAsync(submission);
            }
            catch (Exception ex)
            {
                // e.g. a dose already on file, or a date before birth
                return BadRequest(new { message = ex.Message });
            }

            var child = await _context.Children.FindAsync(submission.ChildID);
            await _audit.LogAsync("Vaccination", "Create",
                child != null ? $"Child – {child.FirstName} {child.LastName}" : $"Child {submission.ChildID}",
                $"Encoded {submission.Vaccinations?.Count ?? 0} historical vaccination record(s) from the Yellow Book.");
            return Ok(new { message = "Historical vaccination records saved successfully." });
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var allRecords = await _repository.GetAllAsync();

            // "Pending" and "missed" come from VaccinationTimeline — that's
            // where not-yet-given doses live. VaccinationRecords only ever
            // holds doses that were actually administered.
            var pendingToday = await _context.VaccinationTimelines
                .CountAsync(t => t.Status == "Pending" && t.ScheduledDate >= today && t.ScheduledDate < tomorrow);

            var missedTotal = await _context.VaccinationTimelines
                .CountAsync(t => (t.Status == "Pending" || t.Status == "Missed") && t.ScheduledDate < today);

            return Ok(new
            {
                vaccinatedToday = allRecords.Count(r => r.Status == "Completed" && r.VaccinationDate >= today && r.VaccinationDate < tomorrow),
                pendingToday,
                weeklyTotal = allRecords.Count(r => r.Status == "Completed" && r.VaccinationDate >= weekStart && r.VaccinationDate < tomorrow),
                missedTotal
            });
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("pending-today")]
        public async Task<IActionResult> GetPendingToday()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var records = (await _repository.GetAllAsync())
                .Where(r => r.Status == "Scheduled" && r.VaccinationDate >= today && r.VaccinationDate < tomorrow)
                .Select(r => new
                {
                    recordId = r.VaccinationRecordID,
                    childId = r.ChildID,
                    childName = r.Child != null ? $"{r.Child.FirstName} {r.Child.LastName}".Trim() : "Unknown",
                    vaccineId = r.VaccineID,
                    vaccineName = r.Vaccine != null ? r.Vaccine.VaccineName : "Unknown",
                    doseNumber = r.DoseNumber,
                    vaccinationDate = r.VaccinationDate,
                    status = r.Status
                })
                .ToList();

            return Ok(records);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("completed-today")]
        public async Task<IActionResult> GetCompletedToday()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var records = (await _repository.GetAllAsync())
                .Where(r => r.Status == "Completed" && r.VaccinationDate >= today && r.VaccinationDate < tomorrow)
                .OrderByDescending(r => r.VaccinationDate)
                .Select(r => new
                {
                    recordId = r.VaccinationRecordID,
                    childId = r.ChildID,
                    childName = r.Child != null ? $"{r.Child.FirstName} {r.Child.LastName}".Trim() : "Unknown",
                    vaccineName = r.Vaccine != null ? r.Vaccine.VaccineName : "Unknown",
                    doseNumber = r.DoseNumber,
                    vaccinationDate = r.VaccinationDate
                })
                .ToList();

            return Ok(records);
        }
    }
}