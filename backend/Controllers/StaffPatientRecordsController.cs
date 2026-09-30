using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffPatientRecordsController : ControllerBase
    {
        private readonly StaffPatientRecordsRepository _repository;

        public StaffPatientRecordsController(StaffPatientRecordsRepository repository)
        {
            _repository = repository;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpGet]
        public async Task<IActionResult> GetAllFamilies()
        {
            var families = await _repository.GetAllFamiliesAsync();
            return Ok(families);
        }
/*
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost]
        public async Task<IActionResult> RegisterFamily(RegisterFamilyDto dto)
        {
             var success = await _repository.RegisterFamilyAsync(dto);

    if (!success)
        return BadRequest();

    return Ok(new
    {
        message = "Family registered successfully."
    });
        }
        */
    }

    public class RegisterFamilyDto
    {
    }
}