using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IAccountRepository _accountRepository;
        private readonly AndroidWebAPI.Services.AuditService _audit;

        public AuthController(
            AppDbContext context,
            IConfiguration config,
            IAccountRepository accountRepository,
            AndroidWebAPI.Services.AuditService audit)
        {
            _context = context;
            _config = config;
            _accountRepository = accountRepository;
            _audit = audit;
        }

        // Login attempts are logged against the account's profile (parent or
        // personnel) so the admin Audit Logs page can show who it was.
        private Task LogLoginAsync(Account? account, string identifier, string status, string description)
        {
            string? role = account?.AccountType switch
            {
                "Parent" => "Parent",
                "SystemAdmin" => "SystemAdmin",
                _ => null,
            };

            return _audit.LogAsync("Authentication", "Login",
                $"Account – {account?.Username ?? identifier}",
                description,
                status,
                userId: account?.ReferenceID,
                userName: account == null ? "Unknown" : null,
                role: role);
        }

        // PATCH /api/Users/{userId}/change-password
        // Route is overridden with "~" because AuthController's base route is
        // "api/auth", but the Doctor/Nurse frontend (DoctorAccount.vue) calls
        // /api/Users/{userId}/change-password directly.
        //
        // Personnel (Doctor/Nurse/Staff/Admin) log in through Accounts, not
        // directly through Users — Accounts.PasswordHash is what auth actually
        // checks, keyed by Accounts.ReferenceID == Users.UserID for
        // AccountType == "Personnel". So this updates the Accounts row, not
        // Users.PasswordHash (which is a legacy/unused column on Users).
        [HttpPatch]
        [Route("~/api/Users/{userId}/change-password")]
        public async Task<IActionResult> ChangePersonnelPassword(Guid userId, [FromBody] PersonnelChangePasswordDto dto)
        {
            // Only your own password (the admin resets others from User Management)
            if (AndroidWebAPI.Services.AccessGuard.CallerId(User) != userId)
                return Forbid();

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountType == "Personnel" && a.ReferenceID == userId);
            if (account == null)
                return NotFound(new { message = "Account not found." });

            bool currentOk;
            try { currentOk = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, account.PasswordHash); }
            catch { currentOk = false; }
            if (!currentOk)
                return BadRequest(new { message = "Current password is incorrect." });

            var complexityError = GetPasswordComplexityError(dto.NewPassword);
            if (complexityError != null)
                return BadRequest(new { message = complexityError });

            var ok = await _accountRepository.ChangePasswordAsync(account.AccountID, dto.NewPassword);
            if (!ok)
                return BadRequest(new { message = "Could not update password." });

            await _audit.LogAsync("User Management", "Update", $"Account – {account.Username}",
                "Changed own password.", userId: userId);

            return Ok(new { message = "Password updated." });
        }

        public class PersonnelChangePasswordDto
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        // Standard complexity rule: 8+ chars, at least one uppercase, one
        // lowercase, one digit, one special character.
        public static string? GetPasswordComplexityError(string? password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return "Password must be at least 8 characters long.";
            if (!password.Any(char.IsUpper))
                return "Password must include at least one uppercase letter.";
            if (!password.Any(char.IsLower))
                return "Password must include at least one lowercase letter.";
            if (!password.Any(char.IsDigit))
                return "Password must include at least one number.";
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                return "Password must include at least one special character.";
            return null;
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordDto dto)
        {
            // =====================================================
            // 1. VALIDATE REQUEST
            // =====================================================

            if (dto == null)
            {
                return BadRequest(new
                {
                    message = "Request body is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
            {
                return BadRequest(new
                {
                    message = "Current password is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return BadRequest(new
                {
                    message = "New password is required."
                });
            }

            var complexityError = GetPasswordComplexityError(dto.NewPassword);
            if (complexityError != null)
            {
                return BadRequest(new
                {
                    message = complexityError
                });
            }


            // =====================================================
            // 2. GET ACCOUNT ID FROM JWT
            // =====================================================

            var accountIdClaim = User.FindFirst("AccountID")?.Value;

            if (string.IsNullOrWhiteSpace(accountIdClaim))
            {
                return Unauthorized(new
                {
                    message = "Account ID was not found in the authentication token."
                });
            }

            if (!Guid.TryParse(accountIdClaim, out Guid accountId))
            {
                return Unauthorized(new
                {
                    message = "Invalid account ID in authentication token."
                });
            }


            // =====================================================
            // 3. FIND ACCOUNT
            // =====================================================

            var account = await _accountRepository.GetByIdAsync(accountId);

            if (account == null)
            {
                return NotFound(new
                {
                    message = "Account not found."
                });
            }

            if (!account.Status)
            {
                return Unauthorized(new
                {
                    message = "This account is inactive."
                });
            }


            // =====================================================
            // 4. VERIFY CURRENT / TEMPORARY PASSWORD
            // =====================================================

            if (string.IsNullOrWhiteSpace(account.PasswordHash))
            {
                return BadRequest(new
                {
                    message = "Account does not have a valid password."
                });
            }

            bool passwordCorrect;

            try
            {
                passwordCorrect = BCrypt.Net.BCrypt.Verify(
                    dto.CurrentPassword,
                    account.PasswordHash
                );
            }
            catch
            {
                passwordCorrect = false;
            }

            if (!passwordCorrect)
            {
                return BadRequest(new
                {
                    message = "Current password is incorrect."
                });
            }


            // =====================================================
            // 5. PREVENT SAME PASSWORD
            // =====================================================

            bool samePassword;

            try
            {
                samePassword = BCrypt.Net.BCrypt.Verify(
                    dto.NewPassword,
                    account.PasswordHash
                );
            }
            catch
            {
                samePassword = false;
            }

            if (samePassword)
            {
                return BadRequest(new
                {
                    message =
                        "New password must be different from the current password."
                });
            }


            // =====================================================
            // 6. CHANGE PASSWORD
            // =====================================================

            var changed = await _accountRepository.ChangePasswordAsync(
                account.AccountID,
                dto.NewPassword
            );

            if (!changed)
            {
                return StatusCode(500, new
                {
                    message = "Unable to change password."
                });
            }

            await _audit.LogAsync("User Management", "Update", $"Account – {account.Username}",
                "Changed password (first-login / self-service).", userId: account.ReferenceID);


            // =====================================================
            // 7. DETERMINE USER ROLE
            // =====================================================

            string role;

            if (account.AccountType.Equals(
                "Parent",
                StringComparison.OrdinalIgnoreCase))
            {
                role = "Parent";
            }
            else if (account.AccountType.Equals(
                "Personnel",
                StringComparison.OrdinalIgnoreCase))
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserID == account.ReferenceID);

                if (user == null)
                {
                    return NotFound(new
                    {
                        message = "Personnel record not found."
                    });
                }

                // Same role names as Login returns
                role = RoleForPosition(user.Position);
            }
            else
            {
                role = account.AccountType;
            }


            // =====================================================
            // 8. SUCCESS
            // =====================================================

            return Ok(new
            {
                message = "Password changed successfully.",
                accountID = account.AccountID,
                referenceID = account.ReferenceID,
                accountType = account.AccountType,
                role = role,
                mustChangePassword = false
            });
        }

        // =========================================================
        // CAPTCHA
        // GET /api/auth/captcha  -> { captchaId, image }
        // A new picture for the Sign In / Forgot Password pages.
        // =========================================================

        [AllowAnonymous]
        [HttpGet("captcha")]
        public IActionResult GetCaptcha([FromServices] AndroidWebAPI.Services.Captcha captcha)
        {
            if (!captcha.Enabled) return Ok(new { enabled = false });
            var (id, image) = captcha.Create();
            Response.Headers.CacheControl = "no-store";
            return Ok(new { enabled = true, captchaId = id, image });
        }

        // =========================================================
        // UNIFIED LOGIN
        // POST /api/auth/login
        // =========================================================

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginDto dto,
            [FromServices] AndroidWebAPI.Services.Captcha captcha)
        {
            // Checked first, so a bot can't even test passwords without it
            if (!captcha.Check(dto.CaptchaId, dto.CaptchaAnswer))
            {
                return BadRequest(new
                {
                    message = AndroidWebAPI.Services.Captcha.WrongAnswer,
                    captcha = true
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Identifier))
            {
                return BadRequest(new
                {
                    message = "Username or email is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    message = "Password is required."
                });
            }

            var identifier = dto.Identifier.Trim();

            // =====================================================
            // 1. FIND ACCOUNT
            //
            // Accounts.Username holds:
            //   - the parent's email, for AccountType = "Parent"
            //   - the admin-assigned username, for AccountType = "Personnel"
            //
            // So a direct Username match covers "login by username" for
            // personnel AND "login by email" for parents. It does NOT
            // cover "login by email" for personnel, since their email
            // lives on the linked Users record, not on Accounts. The
            // two fallback lookups below handle that case.
            // =====================================================

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a =>
                    a.Username == identifier);

            if (account == null)
            {
                var parentMatch = await _context.Parents
                    .FirstOrDefaultAsync(p => p.Email == identifier);

                if (parentMatch != null)
                {
                    account = await _context.Accounts.FirstOrDefaultAsync(a =>
                        a.AccountType == "Parent" &&
                        a.ReferenceID == parentMatch.ParentID);
                }
            }

            if (account == null)
            {
                var userMatch = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == identifier);

                if (userMatch != null)
                {
                    account = await _context.Accounts.FirstOrDefaultAsync(a =>
                        a.AccountType == "Personnel" &&
                        a.ReferenceID == userMatch.UserID);
                }
            }

            if (account == null)
            {
                await LogLoginAsync(null, identifier, "Failed", "Login attempt with an unknown username or email.");

                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            // =====================================================
            // 2. CHECK ACCOUNT STATUS
            // =====================================================

            if (!account.Status)
            {
                return Unauthorized(new
                {
                    message = "Account is inactive."
                });
            }

            // =====================================================
            // 3. CHECK LOCK
            // =====================================================

            if (account.LockedUntil.HasValue &&
                account.LockedUntil.Value > DateTime.Now)
            {
                await LogLoginAsync(account, identifier, "Warning", "Login attempt while the account is temporarily locked.");

                return Unauthorized(new
                {
                    message = "Account is temporarily locked. Please try again later."
                });
            }

            // =====================================================
            // 4. CHECK PASSWORD
            // =====================================================

            if (string.IsNullOrWhiteSpace(account.PasswordHash))
            {
                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            bool passwordValid;

            try
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(
                    dto.Password,
                    account.PasswordHash
                );
            }
            catch
            {
                passwordValid = false;
            }

            if (!passwordValid)
            {
                account.FailedLoginAttempts++;

                if (account.FailedLoginAttempts >= 5)
                {
                    account.LockedUntil =
                        DateTime.Now.AddMinutes(15);

                    account.FailedLoginAttempts = 0;
                }

                await _context.SaveChangesAsync();

                await LogLoginAsync(account, identifier, "Failed",
                    account.LockedUntil > DateTime.Now
                        ? "Incorrect password — account locked for 15 minutes after 5 failed attempts."
                        : "Failed login attempt due to an incorrect password.");

                return Unauthorized(new
                {
                    message = "Invalid username/email or password."
                });
            }

            // =====================================================
            // 5. SUCCESSFUL LOGIN
            // =====================================================

            account.FailedLoginAttempts = 0;
            account.LockedUntil = null;
            account.LastLogin = DateTime.Now;

            await _context.SaveChangesAsync();

            await LogLoginAsync(account, identifier, "Success", "Signed in successfully.");

            // =====================================================
            // 6. PARENT ACCOUNT
            // =====================================================

            if (account.AccountType == "Parent")
            {
                var parent = await _context.Parents
                    .FirstOrDefaultAsync(p =>
                        p.ParentID == account.ReferenceID);

                if (parent == null)
                {
                    return Unauthorized(new
                    {
                        message = "Parent profile not found."
                    });
                }

                var token = GenerateJwtToken(
                    account,
                    parent.ParentID,
                    parent.Email,
                    "Parent"
                );

                return Ok(new
                {
                    token,

                    accountID = account.AccountID,

                    referenceID = account.ReferenceID,

                    accountType = account.AccountType,

                    role = "Parent",

                    user = new
                    {
                        parentID = parent.ParentID,
                        firstName = parent.FirstName,
                        middleName = parent.MiddleName,
                        lastName = parent.LastName,
                        email = parent.Email,
                        contactNo = parent.ContactNo,
                        barangayNo = parent.BarangayNo,
                        address = parent.Address,
                        // null until the parent accepts the Data Privacy Notice
                        privacyConsentAt = parent.PrivacyConsentAt
                    },

                    mustChangePassword =
                        account.MustChangePassword,

                    temporaryPasswordExpiresAt =
                        parent.TemporaryPasswordExpiresAt,

                    lastLogin = account.LastLogin
                });
            }

            // =====================================================
            // 7. PERSONNEL ACCOUNT
            // =====================================================

   // =====================================================
// 7. PERSONNEL ACCOUNT
// =====================================================

if (account.AccountType == "Personnel")
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u =>
            u.UserID == account.ReferenceID);

    if (user == null)
    {
        return Unauthorized(new
        {
            message = "Personnel profile not found."
        });
    }

    // Position decides the portal: Doctor -> Admin, Nurse -> Staff.
    string role = RoleForPosition(user.Position);

    var token = GenerateJwtToken(
        account,
        user.UserID,
        user.Email,
        role
    );

    return Ok(new
    {
        token,

        accountID = account.AccountID,

        referenceID = account.ReferenceID,

        accountType = account.AccountType,

        role = role,

        user = new
        {
            userID = user.UserID,
            firstName = user.FirstName,
            middleName = user.MiddleName,
            lastName = user.LastName,
            username = user.Username,
            email = user.Email,
            contactNo = user.ContactNo,
            prcNo = user.PRCNo,
            accountStatus = user.AccountStatus,

            userType = user.UserType,
            position = user.Position
        },

        mustChangePassword =
            account.MustChangePassword,

        temporaryPasswordExpiresAt =
            (DateTime?)null,

        lastLogin = account.LastLogin
    });
}

 // =====================================================
// 8. SYSTEM ADMIN ACCOUNT
// =====================================================

if (account.AccountType == "SystemAdmin")
{
    var token = GenerateJwtToken(
        account,
        account.ReferenceID,
        null,
        "SystemAdmin"
    );

    return Ok(new
    {
        token,

        accountID = account.AccountID,

        referenceID = account.ReferenceID,

        accountType = account.AccountType,

        role = "SystemAdmin",

        user = new
        {
            username = account.Username
        },

        mustChangePassword =
            account.MustChangePassword,

        temporaryPasswordExpiresAt =
            (DateTime?)null,

        lastLogin = account.LastLogin
    });
}

            return Unauthorized(new
            {
                message = "Unsupported account type."
            });
        }

        private static string RoleForPosition(string? position) =>
            AndroidWebAPI.Services.Roles.ForPosition(position);

        // =========================================================
        // JWT
        // =========================================================

        private string GenerateJwtToken(
            Account account,
            Guid referenceID,
            string? email,
            string role)
        {
            var key = GetJwtKey();

            var credentials =
                new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256
                );

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    account.AccountID.ToString()
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                ),

                new Claim(
                    "AccountID",
                    account.AccountID.ToString()
                ),

                new Claim(
                    "ReferenceID",
                    referenceID.ToString()
                ),

                new Claim(
                    "AccountType",
                    account.AccountType
                )
            };

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Email,
                        email
                    )
                );
            }

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // =========================================================
        // JWT KEY
        // =========================================================

        private SymmetricSecurityKey GetJwtKey()
        {
            var jwtKey = _config["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "JWT Key is not configured."
                );
            }

            return new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            );
        }
    }

    // =============================================================
    // LOGIN DTO
    // =============================================================

    public class LoginDto
    {
        public string Identifier { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        // From GET /api/auth/captcha and what the person typed
        public string? CaptchaId { get; set; }

        public string? CaptchaAnswer { get; set; }
    }


}