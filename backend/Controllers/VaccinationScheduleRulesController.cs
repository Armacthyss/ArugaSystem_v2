    using Microsoft.AspNetCore.Mvc;
    using AndroidWebAPI.Data;
    using AndroidWebAPI.Models;
    using AndroidWebAPI.DTOs;

    namespace AndroidWebAPI.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class VaccinationScheduleRulesController : ControllerBase
        {
            private readonly IVaccinationScheduleRuleRepository _repository;

            public VaccinationScheduleRulesController(IVaccinationScheduleRuleRepository repository)
            {
                _repository = repository;
            }

            [HttpGet]
            public async Task<IActionResult> GetAll()
            {
                return Ok(await _repository.GetAllAsync());
            }

            [HttpGet("vaccine/{vaccineId}")]
            public async Task<IActionResult> GetByVaccine(int vaccineId)
            {
                return Ok(await _repository.GetByVaccineAsync(vaccineId));
            }

            [HttpGet("{vaccineId}/{doseNumber}")]
            public async Task<IActionResult> GetRule(int vaccineId, int doseNumber)
            {
                var rule = await _repository.GetRuleAsync(vaccineId, doseNumber);

                if (rule == null)
                    return NotFound();

                return Ok(rule);
            }

            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
            [HttpPost]
public async Task<IActionResult> Create(CreateVaccinationScheduleRuleDto dto)
{
    var rule = new VaccinationScheduleRule
    {
        VaccineID = dto.VaccineID,
        DoseNumber = dto.DoseNumber,
        MinimumAgeDays = dto.MinimumAgeDays,
        RecommendedAgeDays = dto.RecommendedAgeDays,
        IntervalFromPreviousDoseDays = dto.IntervalFromPreviousDoseDays,
        SequenceOrder = dto.SequenceOrder,
        IsRequired = dto.IsRequired,
        CreatedAt = DateTime.UtcNow
    };

    await _repository.AddAsync(rule);

    return CreatedAtAction(
        nameof(GetRule),
        new
        {
            vaccineId = rule.VaccineID,
            doseNumber = rule.DoseNumber
        },
        rule
    );
}



[Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
[HttpPut]
public async Task<IActionResult> Update(UpdateVaccinationScheduleRuleDto dto)
{
    var existing = await _repository.GetRuleAsync(
        dto.VaccineID,
        dto.DoseNumber
    );

    if (existing == null)
        return NotFound();

    existing.VaccineID = dto.VaccineID;
    existing.DoseNumber = dto.DoseNumber;
    existing.MinimumAgeDays = dto.MinimumAgeDays;
    existing.RecommendedAgeDays = dto.RecommendedAgeDays;
    existing.IntervalFromPreviousDoseDays = dto.IntervalFromPreviousDoseDays;
    existing.SequenceOrder = dto.SequenceOrder;
    existing.IsRequired = dto.IsRequired;
    existing.UpdatedAt = DateTime.UtcNow;

    await _repository.UpdateAsync(existing);

    return NoContent();
}

            [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Admin)]
            [HttpDelete("{ruleId}")]
            public async Task<IActionResult> Delete(int ruleId)
            {
                await _repository.DeleteAsync(ruleId);
                return NoContent();
            }
        }
    }