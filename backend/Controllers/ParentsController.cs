using AndroidWebAPI.Data;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParentsController : ControllerBase
    {
        private readonly ParentRepository _parentRepo;
        private readonly IAccountRepository _accountRepository;

        public ParentsController(
            ParentRepository parentRepo,
            IAccountRepository accountRepository)
        {
            _parentRepo = parentRepo;
            _accountRepository = accountRepository;
        }

        private static string GenerateTemporaryPassword()
        {
            const string characters =
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

            return new string(
                Enumerable
                    .Range(0, 10)
                    .Select(_ => characters[System.Security.Cryptography.RandomNumberGenerator.GetInt32(characters.Length)])
                    .ToArray()
            );
        }

// ── CREATE: POST /api/Parents ─────────────────────────────
[Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
[HttpPost]
public async Task<IActionResult> CreateParent(
    [FromBody] CreateParentDto dto,
    [FromServices] AndroidWebAPI.Services.MessageSender sender,
    [FromServices] AndroidWebAPI.Services.AuditService audit)
{
    try
    {
        // Validation
        if (string.IsNullOrWhiteSpace(dto.FirstName))
            return BadRequest(new { message = "FirstName is required." });

        if (string.IsNullOrWhiteSpace(dto.LastName))
            return BadRequest(new { message = "LastName is required." });

        if (string.IsNullOrWhiteSpace(dto.ContactNo))
            return BadRequest(new { message = "ContactNo is required." });

        if (!AndroidWebAPI.Services.Barangays.IsServed(dto.BarangayNo))
            return BadRequest(new { message = AndroidWebAPI.Services.Barangays.Error });

        // Data Privacy Act (RA 10173): the parent/guardian must agree to
        // their information being kept in Aruga before it is saved.
        if (!dto.PrivacyConsent)
            return BadRequest(new { message = "The parent or guardian must agree to the Data Privacy Notice before they can be registered." });

        // Email (and therefore login) is only required when this
        // guardian is meant to have portal access.
        if (dto.CreateLogin && string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email is required when creating a login for this guardian." });

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            // Accounts.Username = the parent's email for this account type,
            // so it must be unique across ALL accounts (parent + personnel).
            var existingAccount = await _accountRepository.GetByUsernameAsync(dto.Email);

            if (existingAccount != null)
            {
                return Conflict(new
                {
                    message = "An account with this email already exists."
                });
            }
        }

        // Password/login handling. A contact-only guardian
        // (CreateLogin = false) gets no password and no Accounts row.
        string? temporaryPassword = null;
        string? passwordHash = null;
        bool mustChangePassword = false;
        DateTime? temporaryPasswordExpiresAt = null;

        if (dto.CreateLogin)
        {
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                // Client supplied a real password — validate and use it.
                var complexityError = AuthController.GetPasswordComplexityError(dto.Password);
                if (complexityError != null)
                    return BadRequest(new { message = complexityError });

                passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                // Staff typed this password in directly — it's still an
                // account someone other than the parent set up, so force
                // the parent to set their own private password on first
                // login, same as the auto-generated-temp-password path
                // below. No expiry here since this isn't a time-boxed
                // temporary password.
                mustChangePassword = true;
                temporaryPasswordExpiresAt = null;
            }
            else
            {
                // No password supplied — fall back to the old
                // generate-a-temporary-password behavior.
                temporaryPassword = GenerateTemporaryPassword();
                passwordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
                mustChangePassword = true;
                temporaryPasswordExpiresAt = DateTime.Now.AddHours(24);
            }
        }

        var parent = new Parent
        {
            ParentID = Guid.NewGuid(),

            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,

            Email = dto.Email,
            ContactNo = dto.ContactNo,

            BarangayNo = dto.BarangayNo,
            Address = dto.Address,

            // Null for a contact-only guardian (CreateLogin = false).
            PasswordHash = passwordHash,

            ConsentRecordedAt = DateTime.Now,

            MustChangePassword = mustChangePassword,
            TemporaryPasswordExpiresAt = temporaryPasswordExpiresAt
        };

        var created = await _parentRepo.CreateAsync(parent);

        Guid? accountId = null;
        bool emailed = false;
        bool texted = false;

        if (dto.CreateLogin)
        {
            // ─────────────────────────────────────────────
            // Create the matching Accounts row so this parent
            // can log in through /api/auth/login like every
            // other account type.
            // ─────────────────────────────────────────────
            var account = new Account
            {
                AccountID = Guid.NewGuid(),
                Username = dto.Email,
                PasswordHash = passwordHash!,
                AccountType = "Parent",
                ReferenceID = created.ParentID,
                Status = true,
                MustChangePassword = mustChangePassword,
                FailedLoginAttempts = 0,
                LockedUntil = null,
                LastLogin = null,
                CreatedAt = DateTime.Now,
                UpdatedAt = null
            };

            await _accountRepository.CreateAsync(account);
            accountId = account.AccountID;

            // Welcome email with how to sign in. The password is only
            // included when the system made it up; a password the staff
            // typed in was already given to the parent in person.
            emailed = await sender.SendEmailAsync(dto.Email, "Your Aruga parent account is ready",
                $"Hi {dto.FirstName},\n\n" +
                "Leveriza Health Center created your Aruga parent account. With it you can see your child's " +
                "vaccination schedule and records, get reminders before each vaccine, and check in at the clinic.\n\n" +
                $"Sign in with: {dto.Email}\n" +
                (temporaryPassword != null
                    ? $"Temporary password: {temporaryPassword}\n"
                    : "Password: the temporary password the health center staff gave you\n") +
                "\nYou'll be asked to choose your own password the first time you sign in.");

            // Same details by text, so the parent doesn't have to read them
            // off the staff screen. Test accounts are never texted.
            if (temporaryPassword != null)
            {
                bool testAccount = AndroidWebAPI.Services.MessageSender.IsTestAddress(dto.Email);
                texted = await sender.SendSmsAsync(dto.ContactNo,
                    $"Aruga - Leveriza Health Center: your parent account is ready. Sign in with {dto.Email} " +
                    $"Temporary password: {temporaryPassword} You will choose your own password the first time you sign in.",
                    demoRecipient: testAccount) && sender.SmsEnabled && !testAccount;
            }
        }

        await audit.LogAsync("Patient Management", "Create",
            $"Parent – {created.FirstName} {created.LastName}",
            (dto.CreateLogin ? "Registered a parent/guardian with a portal login." : "Registered a parent/guardian (contact only, no login).")
            + " They agreed to the Data Privacy Notice (Data Privacy Act of 2012, RA 10173).");

        return CreatedAtAction(
            nameof(GetParentById),
            new { id = created.ParentID },
            new
            {
                parentID = created.ParentID,
                accountID = accountId,
                firstName = created.FirstName,
                middleName = created.MiddleName,
                lastName = created.LastName,
                email = created.Email,
                contactNo = created.ContactNo,
                barangayNo = created.BarangayNo,
                address = created.Address,

                hasLogin = dto.CreateLogin,
                mustChangePassword = created.MustChangePassword,
                temporaryPasswordExpiresAt =
                    created.TemporaryPasswordExpiresAt,

                // TESTING ONLY — shown to the admin so they can
                // hand it to the parent. Null when the client supplied
                // its own password, or when no login was created.
                temporaryPassword = temporaryPassword,
                emailed,
                texted
            }
        );
    }
    catch (InvalidOperationException ex)
    {
        return Conflict(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return StatusCode(
            500,
            new
            {
                message = "An error occurred: " + ex.Message
            }
        );
    }
}

        // ── READ: GET /api/Parents/{id} ───────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetParentById(Guid id)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, id)) return Forbid();
            var parent = await _parentRepo.GetByIdAsync(id);
            if (parent == null)
                return NotFound(new { message = "Parent not found" });

            return Ok(new
            {
                parentID = parent.ParentID,

                firstName = parent.FirstName,
                middleName = parent.MiddleName,
                lastName = parent.LastName,

                email = parent.Email,
                contactNo = parent.ContactNo,
                barangayNo = parent.BarangayNo,
                address = parent.Address,

                mustChangePassword = parent.MustChangePassword,
                temporaryPasswordExpiresAt =
                    parent.TemporaryPasswordExpiresAt,

                lastLogin = parent.LastLogin,

                consentRecordedAt = parent.ConsentRecordedAt,
                privacyConsentAt = parent.PrivacyConsentAt,

                role = "Parent"

            });
        }

        // ── POST /api/Parents/{id}/privacy-consent ────────────────
        // The parent ticks "I agree" on the Data Privacy Notice (RA 10173)
        // the first time they sign in. Until then the parent portal only
        // shows the notice.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.Parent)]
        [HttpPost("{id:guid}/privacy-consent")]
        public async Task<IActionResult> AcceptPrivacyNotice(
            Guid id,
            [FromServices] AndroidWebAPI.Data.AppDbContext context,
            [FromServices] AndroidWebAPI.Services.AuditService audit)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, id)) return Forbid();
            var parent = await context.Parents.FirstOrDefaultAsync(p => p.ParentID == id);
            if (parent == null) return NotFound(new { message = "Parent not found." });

            if (parent.PrivacyConsentAt == null)
            {
                parent.PrivacyConsentAt = DateTime.Now;
                await context.SaveChangesAsync();
                await audit.LogAsync("Patient Management", "Privacy Consent",
                    $"Parent – {parent.FirstName} {parent.LastName}",
                    "Agreed to the Data Privacy Notice (Data Privacy Act of 2012, RA 10173) in the parent portal.",
                    userId: parent.ParentID);
            }

            return Ok(new { message = "Thank you. Your consent was recorded.", privacyConsentAt = parent.PrivacyConsentAt });
        }

        // ── READ: GET /api/Parents/all ────────────────────────────
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllParents([FromServices] AndroidWebAPI.Data.AppDbContext context)
        {
            var parents = await _parentRepo.GetAllAsync();

            // Each parent's portal login (a contact-only guardian has none)
            var logins = (await context.Accounts
                    .Where(a => a.AccountType == "Parent")
                    .Select(a => new { a.ReferenceID, a.AccountID, a.Username, a.Status })
                    .ToListAsync())
                .GroupBy(a => a.ReferenceID)
                .ToDictionary(g => g.Key, g => g.First());

            var result = parents.Select(p =>
            {
                logins.TryGetValue(p.ParentID, out var login);
                return new
                {
                    parentID = p.ParentID,
                    firstName = p.FirstName,
                    middleName = p.MiddleName,
                    lastName = p.LastName,
                    email = p.Email,
                    contactNo = p.ContactNo,
                    barangayNo = p.BarangayNo,
                    address = p.Address,
                    accountID = login?.AccountID,
                    username = login?.Username,
                    accountStatus = login == null ? "No Login" : login.Status ? "Active" : "Inactive",
                };
            });

            return Ok(result);
        }

        // ── UPDATE: PUT /api/Parents/{id} ─────────────────────────
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateParent(Guid id, [FromBody] UpdateParentDto dto)
        {
            try
            {
                // Get existing parent to verify it exists
                var existing = await _parentRepo.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Parent not found" });

                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.FirstName))
                    return BadRequest(new { message = "FirstName is required." });
                if (string.IsNullOrWhiteSpace(dto.LastName))
                    return BadRequest(new { message = "LastName is required." });
                // Older records may still have a barangay from before the
                // jurisdiction check; only a changed barangay is checked.
                if (dto.BarangayNo != null && dto.BarangayNo != existing.BarangayNo
                    && !AndroidWebAPI.Services.Barangays.IsServed(dto.BarangayNo))
                    return BadRequest(new { message = AndroidWebAPI.Services.Barangays.Error });

                var parent = new Parent
                {
                    ParentID = id,
                    FirstName = dto.FirstName,
                    MiddleName = dto.MiddleName ?? existing.MiddleName,
                    LastName = dto.LastName,
                    Email = dto.Email ?? existing.Email,
                    ContactNo = dto.ContactNo ?? existing.ContactNo,
                    BarangayNo = dto.BarangayNo ?? existing.BarangayNo,
                    Address = dto.Address ?? existing.Address,
                    PasswordHash = existing.PasswordHash // Don't update password here
                };

                var updated = await _parentRepo.UpdateAsync(parent);
                return Ok(new
                {
                    parentID = updated.ParentID,
                    firstName = updated.FirstName,
                    middleName = updated.MiddleName,
                    lastName = updated.LastName,
                    email = updated.Email,
                    contactNo = updated.ContactNo,
                    barangayNo = updated.BarangayNo,
                    address = updated.Address,

                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred: " + ex.Message });
            }
        }

        // ── LOGIN: POST /api/Parents/login ────────────────────────
        // NOTE: This is a legacy, parallel login path that checks
        // Parents.PasswordHash directly and is NOT used by Login.vue
        // (which calls /api/auth/login instead). Now that parents also
        // have an Accounts row, consider retiring this endpoint —
        // it will drift out of sync with Accounts.PasswordHash after
        // any password change or admin reset done via /api/accounts
        // or /api/auth/change-password.
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var parent = await _parentRepo.LoginAsync(
                request.Email,
                request.Password
            );

            if (parent == null)
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });

            // Check temporary password expiration
            if (
                parent.MustChangePassword &&
                parent.TemporaryPasswordExpiresAt.HasValue &&
                parent.TemporaryPasswordExpiresAt.Value < DateTime.Now
            )
            {
                return Unauthorized(new
                {
                    message = "Your temporary password has expired. Please contact the health center."
                });
            }

            return Ok(new
            {
                parentID = parent.ParentID,

                firstName = parent.FirstName,
                middleName = parent.MiddleName,
                lastName = parent.LastName,

                email = parent.Email,
                contactNo = parent.ContactNo,
                barangayNo = parent.BarangayNo,
                address = parent.Address,

                mustChangePassword = parent.MustChangePassword,
                temporaryPasswordExpiresAt =
                    parent.TemporaryPasswordExpiresAt,

                lastLogin = parent.LastLogin,

                role = "Parent"
            });
        }

        // ── CHANGE PASSWORD: PATCH /api/Parents/{id}/change-password
        // DEPRECATED — DO NOT USE FROM NEW FRONTEND CODE.
        // This checks/updates Parents.PasswordHash only, while login and
        // /api/auth/change-password operate on Accounts.PasswordHash (the
        // actual source of truth for authentication). Once a parent has
        // changed their password via /api/auth/change-password, this
        // endpoint's view of "current password" is permanently stale and
        // will reject a password that is actually correct. It also has no
        // [Authorize] check, so any caller can attempt any parent's id.
        // Profilemodal.vue now calls /api/auth/change-password instead.
        // Kept only for backward compatibility until callers are confirmed
        // migrated; consider deleting this action entirely.
        [HttpPatch("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(
            Guid id,
            [FromBody] ChangePasswordDto dto)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, id)) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(new
                {
                    message = "Current password is required."
                });

            if (string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest(new
                {
                    message = "New password is required."
                });

            var complexityError = AuthController.GetPasswordComplexityError(dto.NewPassword);
            if (complexityError != null)
                return BadRequest(new
                {
                    message = complexityError
                });

            var result = await _parentRepo.ChangePasswordAsync(
                id,
                dto.CurrentPassword,
                dto.NewPassword
            );

            if (!result.Success)
                return BadRequest(new
                {
                    message = result.Message
                });

            return Ok(new
            {
                message = result.Message
            });
        }

        // ── DASHBOARD: GET /api/Parents/dashboard/{id} ────────────
        [HttpGet("dashboard/{id}")]
        public async Task<IActionResult> GetDashboard(Guid id)
        {
            if (!AndroidWebAPI.Services.AccessGuard.CanSeeParent(User, id)) return Forbid();
            var data = await _parentRepo.GetDashboardData(id);
            if (data == null)
                return NotFound(new { message = "Parent not found" });
            return Ok(data);
        }
    }

    // ── DTOs still used only by this controller ──────────────────
    public class UpdateParentDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? ContactNo { get; set; }
        public string? BarangayNo { get; set; }
        public string? Address { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}