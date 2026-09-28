using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChildrenController : ControllerBase
    {
        private readonly IChildrenRepository _repository;
        private readonly AuditService _audit;
        private readonly ParentNotifier _notifier;
        private readonly AppDbContext _context;

        public ChildrenController(IChildrenRepository repository, AuditService audit, ParentNotifier notifier, AppDbContext context)
        {
            _repository = repository;
            _audit = audit;
            _notifier = notifier;
            _context = context;
        }

        // ── READ: GET /api/Children/overview ──────────────────────
        // One row per child with everything the admin Patient Management
        // page and dashboards need, computed server-side in a few queries
        // instead of one request per child:
        //   primary parent + contact, dose counts, last dose given,
        //   next dose due, and an overall vaccination status.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview([FromServices] AppDbContext context)
        {
            var today = DateTime.Today;

            var children = await context.Children
                .Include(c => c.ParentRelationships).ThenInclude(r => r.Parent)
                .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
                .ToListAsync();

            var timelines = (await context.VaccinationTimelines
                    .Include(t => t.Vaccine)
                    .ToListAsync())
                .GroupBy(t => t.ChildID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var records = (await context.VaccinationRecords
                    .Include(r => r.Vaccine)
                    .Where(r => r.Status == "Completed")
                    .ToListAsync())
                .GroupBy(r => r.ChildID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = children.Select(c =>
            {
                timelines.TryGetValue(c.ChildID, out var tl);
                records.TryGetValue(c.ChildID, out var recs);
                tl ??= new List<VaccinationTimeline>();
                recs ??= new List<VaccinationRecord>();

                int total = tl.Count;
                int completed = tl.Count(t => t.Status == "Completed");
                var notGiven = tl.Where(t => t.Status == "Pending" || t.Status == "Missed").ToList();
                int overdue = notGiven.Count(t => t.ScheduledDate.Date < today);

                var next = notGiven.OrderBy(t => t.ScheduledDate).FirstOrDefault();
                var last = recs.OrderByDescending(r => r.VaccinationDate).FirstOrDefault();

                string vaccinationStatus =
                    total > 0 && completed == total ? "Fully Vaccinated" :
                    overdue > 0 ? "Delayed" :
                    completed > 0 || recs.Count > 0 ? "Partially Vaccinated" : "Upcoming";

                var links = c.ParentRelationships.Where(r => r.Status == "Active").ToList();
                var primary = links.FirstOrDefault(r => r.IsPrimaryContact) ?? links.FirstOrDefault();

                return new
                {
                    childID = c.ChildID,
                    firstName = c.FirstName,
                    middleName = c.MiddleName,
                    lastName = c.LastName,
                    birthDate = c.BirthDate,
                    sex = c.Sex,
                    placeOfBirth = c.PlaceOfBirth,
                    address = c.Address,
                    barangay = c.Barangay,
                    familyNo = c.FamilyNo,
                    allergies = c.Allergies,
                    existingConditions = c.ExistingConditions,
                    birthWeight = c.BirthWeight,
                    birthHeight = c.BirthHeight,
                    createdAt = c.CreatedAt,
                    parentName = primary?.Parent != null ? $"{primary.Parent.FirstName} {primary.Parent.LastName}".Trim() : null,
                    parentContact = primary?.Parent?.ContactNo,
                    parents = links.Select(r => new
                    {
                        relationshipID = r.RelationshipID,
                        parentID = r.ParentID,
                        name = r.Parent != null ? $"{r.Parent.FirstName} {r.Parent.LastName}".Trim() : null,
                        contactNo = r.Parent?.ContactNo,
                        email = r.Parent?.Email,
                        relationshipType = r.RelationshipType,
                        isPrimaryContact = r.IsPrimaryContact,
                    }),
                    totalDoses = total,
                    completedDoses = completed,
                    overdueDoses = overdue,
                    completion = total > 0 ? (int)Math.Round(completed * 100.0 / total) : 0,
                    vaccinationStatus,
                    lastVaccinationDate = last?.VaccinationDate,
                    lastVaccine = last != null ? $"{last.Vaccine?.VaccineName} (Dose {last.DoseNumber})" : null,
                    nextVaccine = next != null ? $"{next.Vaccine?.VaccineName} (Dose {next.DoseNumber})" : null,
                    nextDueDate = next?.ScheduledDate,
                    // Doses not given yet, for the progress-bar pop-up on
                    // Patient Management (e.g. 14/15 -> "MMR Dose 2, due Mar 3")
                    remainingDoses = notGiven
                        .OrderBy(t => t.ScheduledDate)
                        .Select(t => new
                        {
                            vaccineName = t.Vaccine?.VaccineName ?? $"Vaccine {t.VaccineID}",
                            abbreviation = t.Vaccine?.Abbreviation,
                            doseNumber = t.DoseNumber,
                            scheduledDate = t.ScheduledDate,
                            overdue = t.ScheduledDate.Date < today,
                        }),
                };
            });

            return Ok(result);
        }

        // Birth weight is in kg and birth length in cm; catches the two being
        // swapped or a missing decimal point (e.g. 33 kg instead of 3.3 kg).
        private static string? CheckBirthMeasurements(decimal? weightKg, decimal? heightCm)
        {
            if (weightKg is decimal w && (w < 0.5m || w > 7m))
                return $"Birth weight {w:0.##} kg looks wrong. Enter it in kilograms, between 0.5 and 7 (for example 3.2).";
            if (heightCm is decimal h && (h < 25m || h > 65m))
                return $"Birth height {h:0.#} cm looks wrong. Enter it in centimeters, between 25 and 65 (for example 50).";
            return null;
        }

        // ── CREATE: POST /api/Children ────────────────────────────
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost]
        public async Task<IActionResult> CreateChild([FromBody] CreateChildDto dto)
        {
            // Input validation only — no DB or business logic here.
            if (string.IsNullOrWhiteSpace(dto.FirstName))
                return BadRequest(new { message = "FirstName is required." });
            if (string.IsNullOrWhiteSpace(dto.LastName))
                return BadRequest(new { message = "LastName is required." });
            if (dto.BirthDate == default)
                return BadRequest(new { message = "BirthDate is required." });
            if (dto.Parents == null || !dto.Parents.Any())
                return BadRequest(new { message = "At least one parent link is required." });
            if (CheckBirthMeasurements(dto.BirthWeight, dto.BirthHeight) is string measurementError)
                return BadRequest(new { message = measurementError });
            if (!AndroidWebAPI.Services.Barangays.IsServed(dto.Barangay))
                return BadRequest(new { message = AndroidWebAPI.Services.Barangays.Error });

            try
            {
                var child = await _repository.CreateChildAsync(dto);

                await _audit.LogAsync("Patient Management", "Create",
                    $"Child – {child.FirstName} {child.LastName}",
                    "Registered a new child and generated the vaccination timeline.");

                return CreatedAtAction(nameof(GetChildrenByParent), new { parentId = dto.Parents.First().ParentID }, new
                {
                    childID = child.ChildID,
                    firstName = child.FirstName,
                    middleName = child.MiddleName,
                    lastName = child.LastName,
                    birthDate = child.BirthDate,
                    placeOfBirth = child.PlaceOfBirth,
                    sex = child.Sex,
                    barangay = child.Barangay,
                    familyNo = child.FamilyNo,
                    address = child.Address,
                    healthCenter = child.HealthCenter,
                    allergies = child.Allergies,
                    existingConditions = child.ExistingConditions,
                    birthHeight = child.BirthHeight,
                    birthWeight = child.BirthWeight,
                    parents = child.ParentRelationships.Select(r => new
                    {
                        parentID = r.ParentID,
                        relationshipType = r.RelationshipType,
                        isPrimaryContact = r.IsPrimaryContact,
                        canReceiveNotifications = r.CanReceiveNotifications
                    })
                });
            }
            catch (ParentNotFoundException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred: " + ex.Message });
            }
        }

        // ── UPDATE: PUT /api/Children/{id} ────────────────────────
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateChild(Guid id, [FromBody] UpdateChildDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName))
                return BadRequest(new { message = "FirstName is required." });
            if (string.IsNullOrWhiteSpace(dto.LastName))
                return BadRequest(new { message = "LastName is required." });
            if (CheckBirthMeasurements(dto.BirthWeight, dto.BirthHeight) is string measurementError)
                return BadRequest(new { message = measurementError });

            try
            {
                var before = await _context.Children.AsNoTracking().FirstOrDefaultAsync(c => c.ChildID == id);
                if (before == null)
                    return NotFound(new { message = "Child not found." });

                // Only a changed barangay is checked, so older records can still be edited
                if (dto.Barangay != before.Barangay && !AndroidWebAPI.Services.Barangays.IsServed(dto.Barangay))
                    return BadRequest(new { message = AndroidWebAPI.Services.Barangays.Error });

                var updated = await _repository.UpdateChildAsync(id, dto);

                if (updated == null)
                    return NotFound(new { message = "Child not found." });

                var changes = DescribeChanges(before, updated);

                await _audit.LogAsync("Patient Management", "Update",
                    $"Child – {dto.FirstName} {dto.LastName}",
                    changes.Count == 0
                        ? "Saved the child profile (no changes)."
                        : $"Updated child profile: {string.Join(", ", changes.Select(c => c.Field))}.",
                    oldValue: changes.Count == 0 ? null : string.Join("; ", changes.Select(c => $"{c.Field}: {c.Old}")),
                    newValue: changes.Count == 0 ? null : string.Join("; ", changes.Select(c => $"{c.Field}: {c.New}")));

                await NotifyRecordChangedAsync(id, updated, changes);

                return Ok(new { message = "Child updated successfully.", changed = changes.Select(c => c.Field) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred: " + ex.Message });
            }
        }

        // ── UPDATE: PATCH /api/Children/{id}/health-notes ─────────
        // Doctors and Nurses may update a child's allergies and existing
        // conditions (e.g. an allergy found at the station). The rest of the
        // profile stays with the Admission Staff and the Administrator.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpPatch("{id}/health-notes")]
        public async Task<IActionResult> UpdateHealthNotes(Guid id, [FromBody] AndroidWebAPI.DTOs.UpdateHealthNotesDto dto)
        {
            static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            string? allergies = Clean(dto.Allergies), conditions = Clean(dto.ExistingConditions);
            if ((allergies?.Length ?? 0) > 500 || (conditions?.Length ?? 0) > 500)
                return BadRequest(new { message = "Allergies and existing conditions can each be up to 500 characters." });

            var before = await _context.Children.AsNoTracking().FirstOrDefaultAsync(c => c.ChildID == id);
            var child = await _context.Children.FirstOrDefaultAsync(c => c.ChildID == id);
            if (before == null || child == null)
                return NotFound(new { message = "Child not found." });

            child.Allergies = allergies;
            child.ExistingConditions = conditions;
            child.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var changes = DescribeChanges(before, child);
            if (changes.Count > 0)
            {
                await _audit.LogAsync("Patient Management", "Update",
                    $"Child – {child.FirstName} {child.LastName}",
                    $"Updated health notes: {string.Join(", ", changes.Select(c => c.Field))}.",
                    oldValue: string.Join("; ", changes.Select(c => $"{c.Field}: {c.Old}")),
                    newValue: string.Join("; ", changes.Select(c => $"{c.Field}: {c.New}")));

                await NotifyRecordChangedAsync(id, child, changes);
            }

            return Ok(new
            {
                message = changes.Count > 0 ? "Health notes updated." : "No changes.",
                allergies = child.Allergies,
                existingConditions = child.ExistingConditions,
                changed = changes.Select(c => c.Field),
            });
        }

        // Objective 3 of the study: parents get a confirmation whenever their
        // child's information is changed.
        private async Task NotifyRecordChangedAsync(Guid id, Child updated, List<(string Field, string Old, string New)> changes)
        {
            if (changes.Count == 0) return;

            var tally = new ParentNotifier.Delivery();
            var lines = string.Join("\n", changes.Select(c => $"• {c.Field}: {c.Old} → {c.New}"));
            bool rescheduled = changes.Any(c => c.Field == "Birth date");

            // Text: the new values if they fit in one SMS, else just which fields
            // changed (the full old → new list is in the app and the email).
            const string ask = "If this is wrong, please tell the health center.";
            string sms = $"Leveriza Health Center: {updated.FirstName}'s record was updated. " +
                         $"{string.Join("; ", changes.Select(c => $"{c.Field}: {c.New}"))}. {ask}";
            if (sms.Length > 160)
                sms = $"Leveriza Health Center: {updated.FirstName}'s record was updated " +
                      $"({string.Join(", ", changes.Select(c => c.Field))}). {ask} Details are in your email and the Aruga app.";

            foreach (var parent in await _notifier.ParentsOfChildAsync(id))
            {
                await _notifier.NotifyAsync(parent, new Notification
                {
                    ChildID = id,
                    Type = "RecordUpdated",
                    Title = $"{updated.FirstName}'s record was updated",
                    Message =
                        $"These details in {updated.FirstName} {updated.LastName}'s health record were changed:\n{lines}" +
                        (rescheduled ? "\n\nThe vaccination schedule was adjusted to the corrected birth date. Please check the Schedule page." : "") +
                        "\n\nIf anything looks wrong, please tell Leveriza Health Center on your next visit.",
                    IsRead = false,
                }, tally, smsText: sms);
            }
            await _context.SaveChangesAsync();
        }

        // Field-by-field differences, in words a parent understands
        private static List<(string Field, string Old, string New)> DescribeChanges(Child before, Child after)
        {
            var list = new List<(string Field, string Old, string New)>();

            void Add(string field, object? oldValue, object? newValue)
            {
                string o = Show(oldValue), n = Show(newValue);
                if (!string.Equals(o, n, StringComparison.Ordinal)) list.Add((field, o, n));
            }

            static string Show(object? v) => v switch
            {
                null => "(blank)",
                DateTime d => d.ToString("MMM d, yyyy"),
                string s when string.IsNullOrWhiteSpace(s) => "(blank)",
                decimal m => m.ToString("0.##"),
                _ => v.ToString()!.Trim(),
            };

            Add("First name", before.FirstName, after.FirstName);
            Add("Middle name", before.MiddleName, after.MiddleName);
            Add("Last name", before.LastName, after.LastName);
            Add("Birth date", before.BirthDate.Date, after.BirthDate.Date);
            Add("Sex", before.Sex, after.Sex);
            Add("Place of birth", before.PlaceOfBirth, after.PlaceOfBirth);
            Add("Address", before.Address, after.Address);
            Add("Barangay", before.Barangay, after.Barangay);
            Add("Family No.", before.FamilyNo, after.FamilyNo);
            Add("Allergies", before.Allergies, after.Allergies);
            Add("Existing conditions", before.ExistingConditions, after.ExistingConditions);
            Add("Birth height (cm)", before.BirthHeight, after.BirthHeight);
            Add("Birth weight (kg)", before.BirthWeight, after.BirthWeight);
            return list;
        }

        // ── READ: GET /api/Children/parent/{parentId} ─────────────
        [HttpGet("parent/{parentId}")]
        public async Task<IActionResult> GetChildrenByParent(Guid parentId)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, parentId)) return Forbid();
            try
            {
                var children = await _repository.GetByParentAsync(parentId);

                var result = children.Select(c =>
                {
                    var relationship = c.ParentRelationships
                        .FirstOrDefault(r => r.ParentID == parentId);

                    return new
                    {
                        childID = c.ChildID,
                        firstName = c.FirstName,
                        middleName = c.MiddleName,
                        lastName = c.LastName,
                        birthDate = c.BirthDate,
                        placeOfBirth = c.PlaceOfBirth,
                        allergies = c.Allergies,
                        existingConditions = c.ExistingConditions,
                        sex = c.Sex,
                        healthCenter = c.HealthCenter,
                        barangay = c.Barangay,
                        familyNo = c.FamilyNo,
                        address = c.Address,
                        birthHeight = c.BirthHeight,
                        birthWeight = c.BirthWeight,

                        // Computed: which role does the logged-in parent have for this child
                        myRelationship = relationship?.RelationshipType,
                        isPrimaryContact = relationship?.IsPrimaryContact ?? false,
                        canReceiveNotifications = relationship?.CanReceiveNotifications ?? false
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred: " + ex.Message });
            }
        }

        // ── READ: GET /api/Children/all ────────────────────────────
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("all")]
public async Task<IActionResult> GetAllChildren()
{
    try
    {
        var children = await _repository.GetAllAsync();

        var result = children.Select(c => new
        {
            childID = c.ChildID,
            firstName = c.FirstName,
            middleName = c.MiddleName,
            lastName = c.LastName,
            birthDate = c.BirthDate,
            placeOfBirth = c.PlaceOfBirth,
            address = c.Address,
            healthCenter = c.HealthCenter,
            barangay = c.Barangay,
            familyNo = c.FamilyNo,
            sex = c.Sex,
            allergies = c.Allergies,
            existingConditions = c.ExistingConditions,
            birthHeight = c.BirthHeight,
            birthWeight = c.BirthWeight,
            parentName = GetPrimaryParentName(c),

            parents = c.ParentRelationships.Where(r => r.Status == "Active").Select(r => new
            {
                parentID = r.ParentID,
                parentName = r.Parent != null ? $"{r.Parent.FirstName} {r.Parent.LastName}".Trim() : null,
                relationshipType = r.RelationshipType,
                isPrimaryContact = r.IsPrimaryContact,
                canReceiveNotifications = r.CanReceiveNotifications
            })
        });

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            message = "An error occurred while retrieving children.",
            error = ex.Message
        });
    }
}

// ── READ: GET /api/Children/{id} ─────────────────────────────
[HttpGet("{id}")]
public async Task<IActionResult> GetChildById(Guid id)
{
    if (!await AndroidWebAPI.Services.AccessGuard.CanSeeChildAsync(User, _context, id)) return Forbid();
    try
    {
        var children = await _repository.GetAllAsync();

        var child = children.FirstOrDefault(c => c.ChildID == id);

        if (child == null)
        {
            return NotFound(new
            {
                message = "Child not found."
            });
        }

        var result = new
        {
            childID = child.ChildID,
            firstName = child.FirstName,
            middleName = child.MiddleName,
            lastName = child.LastName,
            birthDate = child.BirthDate,
            placeOfBirth = child.PlaceOfBirth,
            address = child.Address,
            healthCenter = child.HealthCenter,
            allergies = child.Allergies,
            existingConditions = child.ExistingConditions,
            barangay = child.Barangay,
            familyNo = child.FamilyNo,
            sex = child.Sex,
            birthHeight = child.BirthHeight,
            birthWeight = child.BirthWeight,
            parentName = GetPrimaryParentName(child),

            parents = child.ParentRelationships.Where(r => r.Status == "Active").Select(r => new
            {
                parentID = r.ParentID,
                parentName = r.Parent != null ? $"{r.Parent.FirstName} {r.Parent.LastName}".Trim() : null,
                relationshipType = r.RelationshipType,
                isPrimaryContact = r.IsPrimaryContact,
                canReceiveNotifications = r.CanReceiveNotifications
            })
        };

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            message = "An error occurred while retrieving the child.",
            error = ex.Message
        });
    }
}

// Primary contact's display name — falls back to the first linked parent
// if none is flagged primary. Mirrors the same lookup used for vaccination
// notifications and the Vaccination Records list.
private static string? GetPrimaryParentName(Child child)
{
    var relationship = child.ParentRelationships?.FirstOrDefault(r => r.IsPrimaryContact && r.Status == "Active")
        ?? child.ParentRelationships?.FirstOrDefault(r => r.Status == "Active");

    return relationship?.Parent != null
        ? $"{relationship.Parent.FirstName} {relationship.Parent.LastName}".Trim()
        : null;
}
    }
}