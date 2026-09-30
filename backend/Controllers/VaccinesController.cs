using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VaccinesController : ControllerBase
    {
        private readonly VaccineRepository _repository;
        private readonly VaccineDoseRepository _doseRepository;

        public VaccinesController(VaccineRepository repository, VaccineDoseRepository doseRepository)
        {
            _repository = repository;
            _doseRepository = doseRepository;
        }

        // ===========================================
        // GET ALL
        // GET: api/Vaccines
        // ===========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var vaccines = await _repository.GetAllAsync();
            return Ok(vaccines);
        }

        // ===========================================
        // GET ALL WITH DOSES
        // GET: api/Vaccines/with-doses
        // Shape expected by the doctor "Record Vaccination" modal:
        // [{ vaccineID, vaccineName, doses: [{ doseNumber, minIntervalDays }] }]
        // This route was previously missing entirely, so requests fell
        // through to Get(int id) below and failed model binding on
        // "with-doses" (400 "The value 'with-doses' is not valid.").
        // ===========================================
        [HttpGet("with-doses")]
        public async Task<IActionResult> GetAllWithDoses()
        {
            var vaccines = await _repository.GetAllAsync();
            var doses = await _doseRepository.GetAllAsync();

            var result = vaccines.Select(v => new
            {
                vaccineID = v.VaccineID,
                vaccineName = v.VaccineName,
                abbreviation = v.Abbreviation,
                // Oral / Intradermal / Intramuscular...: which injection sites fit
                administrationRoute = v.AdministrationRoute,
                doses = doses
                    .Where(d => d.VaccineID == v.VaccineID)
                    .OrderBy(d => d.DoseNumber)
                    .Select(d => new
                    {
                        doseNumber = d.DoseNumber,
                        minIntervalDays = d.MinIntervalDays
                    })
            });

            return Ok(result);
        }

        // ===========================================
        // GET BY ID
        // GET: api/Vaccines/{id}
        // ===========================================
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var vaccine = await _repository.GetByIdAsync(id);

            if (vaccine == null)
                return NotFound();

            return Ok(vaccine);
        }

        // ===========================================
        // CREATE
        // POST: api/Vaccines
        // ===========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Vaccine vaccine)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _repository.CreateAsync(vaccine);

            return CreatedAtAction(
                nameof(Get),
                new { id = created.VaccineID },
                created);
        }

        // ===========================================
        // UPDATE
        // PUT: api/Vaccines/{id}
        // ===========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Vaccine vaccine)
        {
            if (id != vaccine.VaccineID)
                return BadRequest("ID mismatch.");

            var existing = await _repository.GetByIdAsync(id);

            if (existing == null)
                return NotFound();

            var updated = await _repository.UpdateAsync(vaccine);

            return Ok(updated);
        }

        // ===========================================
        // DELETE
        // DELETE: api/Vaccines/{id}
        // Only a vaccine nobody has used yet can be deleted (e.g. one added
        // by mistake). Once it is in a child's schedule or records, or has
        // stock batches, it is kept for history: deactivate it instead.
        // ===========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(
            int id,
            [FromServices] AppDbContext context,
            [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            var vaccine = await context.Vaccines.FindAsync(id);
            if (vaccine == null)
                return NotFound(new { message = "Vaccine not found." });

            int records = await context.VaccinationRecords.CountAsync(r => r.VaccineID == id);
            int schedules = await context.VaccinationTimelines.Where(t => t.VaccineID == id).Select(t => t.ChildID).Distinct().CountAsync();
            int batches = await context.VaccineInventory.CountAsync(i => i.VaccineID == id);

            if (records > 0 || schedules > 0 || batches > 0)
            {
                var uses = new List<string>();
                if (records > 0) uses.Add($"{records} vaccination record(s)");
                if (schedules > 0) uses.Add($"the schedule of {schedules} child(ren)");
                if (batches > 0) uses.Add($"{batches} stock batch(es)");
                return Conflict(new
                {
                    message = $"{vaccine.VaccineName} is already used in {string.Join(", ", uses)}, so it can't be deleted. Deactivate it instead."
                });
            }

            context.VaccinationScheduleRules.RemoveRange(context.VaccinationScheduleRules.Where(r => r.VaccineID == id));
            context.VaccineDoses.RemoveRange(context.VaccineDoses.Where(d => d.VaccineID == id));
            context.Vaccines.Remove(vaccine);
            await context.SaveChangesAsync();

            await audit.LogAsync("Vaccine Management", "Delete", $"Vaccine – {vaccine.VaccineName}", "Deleted an unused vaccine.");

            return NoContent();
        }
    }
}