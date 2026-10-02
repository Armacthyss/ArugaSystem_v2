using AndroidWebAPI.Data;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    // Personnel profiles (dbo.Users) — Doctors, Nurses, Admission Staff.
    //
    // The password-change route for this same resource
    // (PATCH /api/Users/{userId}/change-password) lives in AuthController
    // because it verifies against Accounts.PasswordHash.
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuditService _audit;

        public UsersController(AppDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // GET /api/Users?position=Doctor,Nurse
        // Directory list used by the Admission Staff "Doctor / Nurse" page.
        // `position` is an optional comma-separated filter on Users.Position.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? position)
        {
            // The clinic's staff only: Super Admins (the system team) aren't listed
            var query = _context.Users.Where(u => u.Position == null || u.Position != AndroidWebAPI.Services.Roles.SuperAdminPosition);

            if (!string.IsNullOrWhiteSpace(position))
            {
                var positions = position
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                query = query.Where(u => positions.Contains(u.Position));
            }

            var users = await query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.AccountType == "Personnel")
                .ToDictionaryAsync(a => a.ReferenceID);

            return Ok(users.Select(u =>
            {
                accounts.TryGetValue(u.UserID, out var account);
                return new
                {
                    userID = u.UserID,
                    firstName = u.FirstName,
                    middleName = u.MiddleName,
                    lastName = u.LastName,
                    username = account?.Username ?? u.Username,
                    email = u.Email,
                    contactNo = u.ContactNo,
                    address = u.Address,
                    position = u.Position,
                    prcNo = u.PRCNo,
                    status = account != null ? (account.Status ? "Active" : "Inactive") : u.AccountStatus,
                    lastLogin = account?.LastLogin,
                };
            }));
        }

        // GET /api/Users/{id}
        // Profile for the Healthcare Worker "My Account" page.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound(new { message = "User not found." });

            return Ok(new
            {
                userID = user.UserID,
                firstName = user.FirstName,
                middleName = user.MiddleName,
                lastName = user.LastName,
                username = user.Username,
                email = user.Email,
                contactNo = user.ContactNo,
                address = user.Address,
                position = user.Position,
                userType = user.UserType,
                prcNo = user.PRCNo,
                accountStatus = user.AccountStatus,
            });
        }

        // PUT /api/Users/{id}
        // Self-service edit from the Healthcare Worker "My Account" page.
        // Only contact details can be changed here — name, PRC license
        // number and role are official records that only the System
        // Administrator may change (PUT /api/Users/{id}/admin below).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateContactInfo(Guid id, [FromBody] UpdateContactInfoDto dto)
        {
            if (AndroidWebAPI.Services.AccessGuard.CallerId(User) != id && !User.IsInRole(AndroidWebAPI.Services.Roles.Admin)) return Forbid();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound(new { message = "User not found." });

            var emailError = await CheckContactAsync(user, dto.Email, dto.ContactNo);
            if (emailError != null) return BadRequest(new { message = emailError });

            user.Email = Clean(dto.Email);
            user.ContactNo = Clean(dto.ContactNo);
            user.Address = Clean(dto.Address);

            await _context.SaveChangesAsync();

            await _audit.LogAsync("User Management", "Update",
                $"Profile – {user.FirstName} {user.LastName}",
                "Updated own contact information.",
                userId: user.UserID);

            return Ok(ToProfile(user));
        }

        // PUT /api/Users/{id}/admin
        // System Administrator edit (User Management): the full profile,
        // including name and PRC license number. Position/role is not
        // changed here.
        // The Super Admin edits Doctor and Super Admin profiles; the Doctor
        // edits the clinic's staff (not the Super Admins).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.AdminOrSuperAdmin)]
        [HttpPut("{id:guid}/admin")]
        public async Task<IActionResult> AdminUpdateProfile(Guid id, [FromBody] AdminUpdateUserDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null) return NotFound(new { message = "User not found." });

            bool targetSuper = AndroidWebAPI.Services.Roles.IsSuperAdminPosition(user.Position);
            bool bySuper = User.IsInRole(AndroidWebAPI.Services.Roles.SuperAdmin);
            if (bySuper ? !(targetSuper || AndroidWebAPI.Services.Roles.IsDoctorPosition(user.Position)) : targetSuper)
                return BadRequest(new { message = "This profile is managed by another user level." });

            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                return BadRequest(new { message = "First and last name are required." });

            var emailError = await CheckContactAsync(user, dto.Email, dto.ContactNo);
            if (emailError != null) return BadRequest(new { message = emailError });

            var before = $"{user.FirstName} {user.LastName}, PRC {user.PRCNo ?? "—"}";

            user.FirstName = dto.FirstName.Trim();
            user.MiddleName = Clean(dto.MiddleName);
            user.LastName = dto.LastName.Trim();
            user.PRCNo = Clean(dto.PRCNo);
            user.Email = Clean(dto.Email);
            user.ContactNo = Clean(dto.ContactNo);
            user.Address = Clean(dto.Address);

            await _context.SaveChangesAsync();

            await _audit.LogAsync("User Management", "Update",
                $"{user.Position} Account – {user.FirstName} {user.LastName}",
                "Administrator updated a staff profile.",
                oldValue: before,
                newValue: $"{user.FirstName} {user.LastName}, PRC {user.PRCNo ?? "—"}");

            return Ok(ToProfile(user));
        }

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // One email and one mobile number per person (parents and staff).
        // Only a changed value is checked, so older records still save.
        private Task<string?> CheckContactAsync(AndroidWebAPI.Models.User user, string? email, string? phone)
        {
            bool emailChanged = !string.Equals(Clean(email), user.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
            bool phoneChanged = AndroidWebAPI.Services.MessageSender.NormalizePhNumber(phone)
                                != AndroidWebAPI.Services.MessageSender.NormalizePhNumber(user.ContactNo);
            return AndroidWebAPI.Services.ContactCheck.DuplicateAsync(_context,
                emailChanged ? email : null, phoneChanged ? phone : null, exceptUser: user.UserID);
        }

        private static object ToProfile(AndroidWebAPI.Models.User user) => new
        {
            message = "Profile updated.",
            userID = user.UserID,
            firstName = user.FirstName,
            middleName = user.MiddleName,
            lastName = user.LastName,
            email = user.Email,
            contactNo = user.ContactNo,
            address = user.Address,
            prcNo = user.PRCNo,
            position = user.Position,
        };
    }

    public class UpdateContactInfoDto
    {
        public string? Email { get; set; }
        public string? ContactNo { get; set; }
        public string? Address { get; set; }
    }

    public class AdminUpdateUserDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? PRCNo { get; set; }
        public string? Email { get; set; }
        public string? ContactNo { get; set; }
        public string? Address { get; set; }
    }
}
