using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VaccineDosesController : ControllerBase
    {
        private readonly VaccineDoseRepository _repository;

        public VaccineDosesController(VaccineDoseRepository repository)
        {
            _repository = repository;
        }

        // ========================================
        // GET ALL
        // GET: api/VaccineDoses
        // ========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var doses = await _repository.GetAllAsync();
            return Ok(doses);
        }

        // ========================================
        // GET BY ID
        // GET: api/VaccineDoses/5
        // ========================================
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var dose = await _repository.GetByIdAsync(id);

            if (dose == null)
                return NotFound();

            return Ok(dose);
        }

        // ========================================
        // GET BY VACCINE
        // GET: api/VaccineDoses/vaccine/1
        // ========================================
        [HttpGet("vaccine/{vaccineId}")]
        public async Task<IActionResult> GetByVaccine(int vaccineId)
        {
            var doses = await _repository.GetByVaccineIdAsync(vaccineId);
            return Ok(doses);
        }

        // ========================================
        // CREATE
        // POST: api/VaccineDoses
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VaccineDose dose)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var created = await _repository.CreateAsync(dose);

            return CreatedAtAction(
                nameof(Get),
                new { id = created.DoseID },
                created);
        }

        // ========================================
        // UPDATE
        // PUT: api/VaccineDoses/5
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] VaccineDose dose)
        {
            if (id != dose.DoseID)
                return BadRequest("ID mismatch.");

            var existing = await _repository.GetByIdAsync(id);

            if (existing == null)
                return NotFound();

            var updated = await _repository.UpdateAsync(dose);

            return Ok(updated);
        }

        // ========================================
        // DELETE
        // DELETE: api/VaccineDoses/5
        // ========================================
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _repository.DeleteAsync(id);

            if (!success)
                return NotFound();

            return Ok(new
            {
                message = "Dose deleted successfully."
            });
        }
    }
}