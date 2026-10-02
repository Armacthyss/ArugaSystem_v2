using AndroidWebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
    public class StaffController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StaffController(AppDbContext context) { _context = context; }

        // GET /api/Staff/status
        // "active"/"occupied"/"inactive" for each Doctor and Nurse.
        // No real login-session tracking exists yet, so:
        //   - inactive = the account is not Active
        //   - occupied = the vaccinator in the vaccination room right now
        //   - active   = everyone else
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var staff = await _context.Users
                .Where(u => u.Position != AndroidWebAPI.Services.Roles.SuperAdminPosition)
                .ToListAsync();

            var inRoom = await _context.ClinicRooms
                .Where(r => r.IsOccupied && r.AssignedDoctorID != null)
                .Select(r => r.AssignedDoctorID!.Value)
                .ToListAsync();

            var result = staff.Select(u => new
            {
                staffID = u.UserID,
                name = $"{u.FirstName} {u.LastName}".Trim(),
                role = u.Position,
                status = u.AccountStatus != "Active"
                    ? "inactive"
                    : inRoom.Contains(u.UserID) ? "occupied" : "active"
            });

            return Ok(result);
        }
    }
}
