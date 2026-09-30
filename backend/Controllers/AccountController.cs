using AndroidWebAPI.Data;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountRepository _accountRepository;
        private readonly AppDbContext _context;
        private readonly AndroidWebAPI.Services.AuditService _audit;

        public AccountsController(
            IAccountRepository accountRepository,
            AppDbContext context,
            AndroidWebAPI.Services.AuditService audit)
        {
            _accountRepository = accountRepository;
            _context = context;
            _audit = audit;
        }

        // Who manages which accounts:
        //   Super Admin    -> Admin / Doctor and Super Admin accounts
        //   Admin / Doctor -> Staff / Nurse and parent accounts
        //   Staff / Nurse  -> parent accounts (a parent at the counter)
        private async Task<bool> CanManageAsync(Account account)
        {
            if (account.AccountType == "Parent")
                return !User.IsInRole(AndroidWebAPI.Services.Roles.SuperAdmin);

            var position = await _context.Users.Where(u => u.UserID == account.ReferenceID)
                .Select(u => u.Position).FirstOrDefaultAsync();
            bool topLevel = AndroidWebAPI.Services.Roles.IsDoctorPosition(position)
                            || AndroidWebAPI.Services.Roles.IsSuperAdminPosition(position);

            if (User.IsInRole(AndroidWebAPI.Services.Roles.SuperAdmin)) return topLevel;
            if (User.IsInRole(AndroidWebAPI.Services.Roles.Admin)) return !topLevel;
            return false;
        }

        private const string NotYours =
            "This account is managed by another user level (Super Admin: Doctors; Doctor: Nurses and parents).";

        // =========================================================
        // GET /api/accounts
        // Unified list for the User Management tables. The Doctor sees the
        // clinic's accounts (everyone except the Super Admins); the Super
        // Admin sees the Doctor and Super Admin accounts only (no families).
        // =========================================================

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.AdminOrSuperAdmin)]
        [HttpGet]
        public async Task<IActionResult> GetAllAccounts()
        {
            bool super = User.IsInRole(AndroidWebAPI.Services.Roles.SuperAdmin);

            var accounts = await _context.Accounts
                .Where(a => !super || a.AccountType == "Personnel")
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var parents = super
                ? new Dictionary<Guid, Parent>()
                : await _context.Parents.ToDictionaryAsync(p => p.ParentID);
            var users = await _context.Users.ToDictionaryAsync(u => u.UserID);

            var result = new List<object>();

            foreach (var account in accounts)
            {
                if (account.AccountType == "Personnel" && users.TryGetValue(account.ReferenceID, out var u))
                {
                    bool topLevel = AndroidWebAPI.Services.Roles.IsDoctorPosition(u.Position)
                                    || AndroidWebAPI.Services.Roles.IsSuperAdminPosition(u.Position);
                    if (super ? !topLevel : AndroidWebAPI.Services.Roles.IsSuperAdminPosition(u.Position))
                        continue;
                }

                if (account.AccountType == "Parent" &&
                    parents.TryGetValue(account.ReferenceID, out var parent))
                {
                    result.Add(new
                    {
                        accountID = account.AccountID,
                        referenceID = account.ReferenceID,
                        firstName = parent.FirstName,
                        middleName = parent.MiddleName,
                        lastName = parent.LastName,
                        username = account.Username,
                        email = parent.Email,
                        contactNo = parent.ContactNo,
                        role = "Parent",
                        status = account.Status ? "Active" : "Inactive",
                        mustChangePassword = account.MustChangePassword,
                        lastLogin = account.LastLogin,
                        createdAt = account.CreatedAt
                    });
                }
                else if (account.AccountType == "Personnel" &&
                         users.TryGetValue(account.ReferenceID, out var user))
                {
                    result.Add(new
                    {
                        accountID = account.AccountID,
                        referenceID = account.ReferenceID,
                        firstName = user.FirstName,
                        middleName = user.MiddleName,
                        lastName = user.LastName,
                        username = account.Username,
                        email = user.Email,
                        contactNo = user.ContactNo,
                        // Position is the real role (Doctor / Nurse / Staff /
                        // Administrator); UserType is a legacy column whose
                        // values ("Admission", ...) don't match the UI's roles.
                        role = string.IsNullOrWhiteSpace(user.Position) ? user.UserType : user.Position,
                        prcNo = user.PRCNo,
                        address = user.Address,
                        status = account.Status ? "Active" : "Inactive",
                        locked = account.LockedUntil > DateTime.Now,
                        mustChangePassword = account.MustChangePassword,
                        lastLogin = account.LastLogin,
                        createdAt = account.CreatedAt
                    });
                }
                // Accounts whose Parent/User record is missing (orphaned) are skipped
                // rather than crashing the whole list.
            }

            return Ok(result);
        }

        // =========================================================
        // POST /api/accounts/personnel
        // =========================================================

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.AdminOrSuperAdmin)]
        [HttpPost("personnel")]
public async Task<IActionResult> CreatePersonnelAccount(
    [FromBody] CreatePersonnelAccountDto dto,
    [FromServices] AndroidWebAPI.Services.MessageSender sender)
{
    if (string.IsNullOrWhiteSpace(dto.FirstName))
        return BadRequest(new { message = "First name is required." });

    if (string.IsNullOrWhiteSpace(dto.LastName))
        return BadRequest(new { message = "Last name is required." });

    if (string.IsNullOrWhiteSpace(dto.Role))
        return BadRequest(new { message = "Role is required." });

    // The Doctor adds Nurses; the Super Admin adds Doctors and Super Admins
    bool bySuperAdmin = User.IsInRole(AndroidWebAPI.Services.Roles.SuperAdmin);
    var allowed = bySuperAdmin ? AndroidWebAPI.Services.Roles.SuperAdminPositions : AndroidWebAPI.Services.Roles.Positions;
    if (!allowed.Contains(dto.Role))
    {
        return BadRequest(new
        {
            message = bySuperAdmin
                ? "Role must be Doctor (Admin) or Super Admin."
                : "You can add Nurse (Staff) accounts. Doctor accounts are added by the Super Admin."
        });
    }
    bool newSuperAdmin = AndroidWebAPI.Services.Roles.IsSuperAdminPosition(dto.Role);

    // =========================================================
    // GENERATE USERNAME AUTOMATICALLY
    // =========================================================

    string username = await GenerateUniqueUsernameAsync(
        dto.FirstName,
        dto.LastName
    );

    // Generate temporary password
    string temporaryPassword = GenerateTemporaryPassword();

    string passwordHash =
        BCrypt.Net.BCrypt.HashPassword(temporaryPassword);

    // =========================================================
    // 1. CREATE USER / PERSONNEL RECORD
    // =========================================================

    var user = new User
    {
        UserID = Guid.NewGuid(),
        FirstName = dto.FirstName,
        MiddleName = dto.MiddleName,
        LastName = dto.LastName,
        Username = username,
        PasswordHash = passwordHash,
        Email = dto.Email,
        ContactNo = dto.ContactNo,
        Address = dto.Address,
        // dbo.Users has a CHECK constraint allowing only 'Doctor' | 'Nurse' |
        // 'Admission' | 'Administrator' here; a Super Admin keeps it NULL.
        // Position (below) is what login actually uses to pick the portal.
        UserType = newSuperAdmin ? null : dto.Role,
        Position = dto.Role,
        PRCNo = newSuperAdmin ? null : dto.LicenseNumber,
        AccountStatus = "Active"
    };

    _context.Users.Add(user);

    await _context.SaveChangesAsync();

    // =========================================================
    // 2. CREATE ACCOUNT RECORD
    // =========================================================

    var account = new Account
    {
        AccountID = Guid.NewGuid(),
        Username = username,
        PasswordHash = passwordHash,
        AccountType = "Personnel",
        ReferenceID = user.UserID,
        Status = true,
        MustChangePassword = true,
        FailedLoginAttempts = 0,
        LockedUntil = null,
        LastLogin = null,
        CreatedAt = DateTime.Now,
        UpdatedAt = null
    };

    await _accountRepository.CreateAsync(account);

    string level = newSuperAdmin ? "Super Admin" : dto.Role == "Doctor" ? "Admin / Doctor" : "Staff / Nurse";

    await _audit.LogAsync("User Management", "Create",
        $"{level} Account – {user.FirstName} {user.LastName}",
        $"Created a new {level} account (username {username}).",
        newValue: $"Status: Active, Role: {level}");

    // Sign-in details to the new staff member (the admin also sees them)
    bool emailed = await sender.SendEmailAsync(dto.Email, "Your Aruga staff account",
        $"Hi {dto.FirstName},\n\nAn Aruga account was created for you at Leveriza Health Center " +
        $"({level}).\n\n" +
        $"Username: {username}\nTemporary password: {temporaryPassword}\n\n" +
        "You'll be asked to choose your own password the first time you sign in.");

    // ...and by text (test accounts are never texted)
    bool texted = await TextTemporaryPasswordAsync(sender, dto.ContactNo, dto.Email,
        $"Aruga - Leveriza Health Center: your staff account is ready. Username: {username} " +
        $"Temporary password: {temporaryPassword} You will choose your own password the first time you sign in.");

    // =========================================================
    // RETURN GENERATED CREDENTIALS
    // =========================================================

    return Ok(new
    {
        message = "Personnel account created successfully.",

        account = new
        {
            account.AccountID,
            account.Username,
            account.AccountType,
            account.ReferenceID,

            user.UserID,
            user.FirstName,
            user.MiddleName,
            user.LastName,
            user.UserType,
            user.PRCNo,
            user.ContactNo,
            user.Email,
            user.Address
        },

        temporaryPassword,
        emailed,
        texted
    });
}

// Texts a new temporary password to the account owner. False when no text
// actually went out (no SMS provider, test account, bad number, daily limit).
private static async Task<bool> TextTemporaryPasswordAsync(
    AndroidWebAPI.Services.MessageSender sender, string? contactNo, string? email, string text)
{
    bool testAccount = AndroidWebAPI.Services.MessageSender.IsTestAddress(email);
    return await sender.SendSmsAsync(contactNo, text, demoRecipient: testAccount)
        && sender.SmsEnabled && !testAccount;
}

         

           

        // =========================================================
        // PATCH /api/accounts/{id}/status
        // Active / Inactive only.
        // =========================================================

        // Nurses register families, so they may also switch a parent's login
        // off and on; see CanManageAsync for the other levels.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin + "," + AndroidWebAPI.Services.Roles.SuperAdmin)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateAccountStatusDto dto)
        {
            var account = await _accountRepository.GetByIdAsync(id);

            if (account == null)
                return NotFound(new { message = "Account not found." });
            if (!await CanManageAsync(account))
                return BadRequest(new { message = NotYours });
            // Deactivating yourself would lock you out of the Admin portal
            if (!dto.Status && User.FindFirst("AccountID")?.Value == account.AccountID.ToString())
                return BadRequest(new { message = "You can't deactivate your own account." });

            bool previous = account.Status;
            account.Status = dto.Status;
            account.UpdatedAt = DateTime.Now;

            await _accountRepository.UpdateAsync(account);

            await _audit.LogAsync("User Management", dto.Status ? "Activate" : "Deactivate",
                $"Account – {account.Username}",
                dto.Status ? "Activated a user account." : "Deactivated a user account.",
                oldValue: $"Status: {(previous ? "Active" : "Inactive")}",
                newValue: $"Status: {(dto.Status ? "Active" : "Inactive")}");

            return Ok(new
            {
                message = dto.Status ? "Account activated." : "Account deactivated.",
                accountID = account.AccountID,
                status = account.Status ? "Active" : "Inactive"
            });
        }

        // =========================================================
        // POST /api/accounts/{id}/unlock
        // Lifts the 15-minute lock after 5 wrong passwords, e.g. a Doctor
        // who needs in right away.
        // =========================================================

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.AdminOrSuperAdmin)]
        [HttpPost("{id}/unlock")]
        public async Task<IActionResult> Unlock(Guid id)
        {
            var account = await _accountRepository.GetByIdAsync(id);
            if (account == null)
                return NotFound(new { message = "Account not found." });
            if (!await CanManageAsync(account))
                return BadRequest(new { message = NotYours });

            account.FailedLoginAttempts = 0;
            account.LockedUntil = null;
            account.UpdatedAt = DateTime.Now;
            await _accountRepository.UpdateAsync(account);

            await _audit.LogAsync("User Management", "Unlock", $"Account – {account.Username}",
                "Unlocked the account after too many wrong passwords.");
            return Ok(new { message = "Account unlocked." });
        }

        // =========================================================
        // POST /api/accounts/{id}/reset-password
        // Admin-triggered reset. Forces MustChangePassword back to true.
        // =========================================================

        // Staff may reset a parent's password (e.g. a parent at the counter
        // who forgot it); see CanManageAsync for the other levels.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin + "," + AndroidWebAPI.Services.Roles.SuperAdmin)]
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(Guid id, [FromServices] AndroidWebAPI.Services.MessageSender sender)
        {
            var account = await _accountRepository.GetByIdAsync(id);

            if (account == null)
                return NotFound(new { message = "Account not found." });
            if (!await CanManageAsync(account))
                return BadRequest(new { message = NotYours });

            string temporaryPassword = GenerateTemporaryPassword();

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            account.MustChangePassword = true;
            // A new password also lifts a lock from wrong tries
            account.FailedLoginAttempts = 0;
            account.LockedUntil = null;
            account.UpdatedAt = DateTime.Now;

            await _accountRepository.UpdateAsync(account);

            await _audit.LogAsync("User Management", "Reset Password",
                $"Account – {account.Username}",
                "Reset the account password to a temporary one (must be changed on next login).");

            // Tell the owner their temporary password, by email and text
            var owner = account.AccountType == "Parent"
                ? await _context.Parents.Where(p => p.ParentID == account.ReferenceID)
                    .Select(p => new { p.Email, p.ContactNo }).FirstOrDefaultAsync()
                : await _context.Users.Where(u => u.UserID == account.ReferenceID)
                    .Select(u => new { u.Email, u.ContactNo }).FirstOrDefaultAsync();
            bool emailed = await sender.SendEmailAsync(owner?.Email, "Your Aruga password was reset",
                $"Leveriza Health Center reset the password for your Aruga account ({account.Username}).\n\n" +
                $"Temporary password: {temporaryPassword}\n\n" +
                "You'll be asked to choose your own password the next time you sign in. " +
                "If you didn't ask for this, please contact Leveriza Health Center.");
            bool texted = await TextTemporaryPasswordAsync(sender, owner?.ContactNo, owner?.Email,
                $"Aruga - Leveriza Health Center reset your password ({account.Username}). " +
                $"Temporary password: {temporaryPassword} You will choose a new one when you sign in.");

            return Ok(new
            {
                message = "Password reset successfully.",
                accountID = account.AccountID,
                temporaryPassword,
                emailed,
                texted
            });
        }

        private async Task<string> GenerateUniqueUsernameAsync(
    string firstName,
    string lastName)
{
    string first = NormalizeName(firstName);
    string last = NormalizeName(lastName);

    string baseUsername = $"{first}_{last}";

    string username = baseUsername;
    int counter = 1;

    while (await _context.Accounts.AnyAsync(a => a.Username == username) ||
           await _context.Users.AnyAsync(u => u.Username == username))
    {
        username = $"{baseUsername}{counter:00}";
        counter++;
    }

    return username;
}

private static string NormalizeName(string name)
{
    return new string(
        name
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray()
    );
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
    }

    public class UpdateAccountStatusDto
    {
        public bool Status { get; set; }
    }
}