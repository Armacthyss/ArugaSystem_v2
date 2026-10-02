using System.Security.Cryptography;
using AndroidWebAPI.Data;
using AndroidWebAPI.Models;
using AndroidWebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Controllers
{
    // Forgot Password, in three steps (used by Login/ForgotPassword.vue):
    //
    //   POST /api/auth/forgot-password/request  { identifier, deliveryMethod }
    //        Sends a 6-digit code to the email or mobile number on file.
    //   POST /api/auth/forgot-password/verify   { identifier, otp }
    //        Checks the code before the new-password form is shown.
    //   POST /api/auth/forgot-password/reset    { identifier, otp, newPassword }
    //        Sets the new password and uses up the code.
    //
    // The identifier is whatever the person logs in with: a parent's email,
    // or a staff member's username or email.
    //
    // Security:
    //   - only a BCrypt hash of the code is stored
    //   - a code lasts 10 minutes and allows 5 wrong tries
    //   - a new code can be requested once a minute, 5 times an hour
    //   - the reply never says whether an account exists, so this page
    //     can't be used to find out who has an account
    [ApiController]
    [Route("api/auth/forgot-password")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class PasswordResetController : ControllerBase
    {
        private const string Purpose = "PasswordReset";
        private const int CodeMinutes = 10;
        private const int MaxAttempts = 5;

        private readonly AppDbContext _context;
        private readonly IAccountRepository _accounts;
        private readonly MessageSender _sender;
        private readonly AuditService _audit;

        public PasswordResetController(AppDbContext context, IAccountRepository accounts, MessageSender sender, AuditService audit)
        {
            _context = context;
            _accounts = accounts;
            _sender = sender;
            _audit = audit;
        }

        public class RequestDto
        {
            public string Identifier { get; set; } = string.Empty;
            public string DeliveryMethod { get; set; } = "email";
            public string? CaptchaId { get; set; }
            public string? CaptchaAnswer { get; set; }
        }

        public class VerifyDto
        {
            public string Identifier { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
        }

        public class ResetDto : VerifyDto
        {
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestCode([FromBody] RequestDto dto, [FromServices] Captcha captcha)
        {
            var identifier = dto.Identifier?.Trim() ?? "";
            if (identifier.Length == 0)
                return BadRequest(new { message = "Enter your email or username first." });

            // Bots asking for codes would use up the SMS load and flood inboxes
            if (!captcha.Check(dto.CaptchaId, dto.CaptchaAnswer))
                return BadRequest(new { message = Captcha.WrongAnswer, captcha = true });

            bool bySms = string.Equals(dto.DeliveryMethod, "sms", StringComparison.OrdinalIgnoreCase);
            string channel = bySms ? "mobile number" : "email address";
            string genericReply =
                $"If an account matches, a 6-digit code was sent to the {channel} registered to it. " +
                $"It expires in {CodeMinutes} minutes.";

            var target = await FindAccountAsync(identifier);
            if (target == null)
            {
                await _audit.LogAsync("Authentication", "Password Reset", $"Account – {identifier}",
                    "Password reset requested for an unknown username or email.", "Failed", userName: "Unknown");
                return Ok(new { message = genericReply });
            }

            var (account, email, phone, name) = target.Value;

            if (!account.Status)
                return Ok(new { message = genericReply });

            var destination = bySms ? MessageSender.NormalizePhNumber(phone) : email;
            if (string.IsNullOrWhiteSpace(destination))
            {
                // This does tell the person the account exists, but they already
                // typed its username/email, and many parents have no email on file,
                // so pointing them to the other channel matters more here.
                return BadRequest(new
                {
                    message = bySms
                        ? "There's no valid mobile number on this account. Try Email instead, or ask the health center to update your number."
                        : "There's no email address on this account. Try SMS instead, or ask the health center to update your email."
                });
            }

            // Rate limits
            var now = DateTime.UtcNow;
            var recent = await _context.AccountOtps
                .Where(o => o.AccountID == account.AccountID && o.Purpose == Purpose && o.CreatedAt > now.AddHours(-1))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            if (recent.Count > 0 && recent[0].CreatedAt > now.AddSeconds(-60))
                return StatusCode(429, new { message = "A code was just sent. Please wait a minute before asking for another one." });
            if (recent.Count >= 5)
                return StatusCode(429, new { message = "Too many codes were requested. Please try again in an hour." });

            // Only the newest code works
            await _context.AccountOtps
                .Where(o => o.AccountID == account.AccountID && o.Purpose == Purpose && !o.IsUsed)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _context.AccountOtps.Add(new AccountOtp
            {
                OTPID = Guid.NewGuid(),
                AccountID = account.AccountID,
                OTPHash = BCrypt.Net.BCrypt.HashPassword(code),
                Purpose = Purpose,
                ExpiresAt = now.AddMinutes(CodeMinutes),
                CreatedAt = now,
            });
            await _context.SaveChangesAsync();

            // Demo accounts (DemoSeed.sql, IDs starting A2A) and test records
            // (…@example.com, …@demo.…) have made-up numbers
            bool demoAccount = account.ReferenceID.ToString().StartsWith("a2a", StringComparison.OrdinalIgnoreCase) ||
                               MessageSender.IsTestAddress(email);

            bool sent = bySms
                ? await _sender.SendSmsAsync(destination,
                    $"Aruga - Leveriza Health Center: your password reset code is {code}. It expires in {CodeMinutes} minutes. Do not share this code.",
                    demoRecipient: demoAccount)
                : await _sender.SendEmailAsync(destination, "Your password reset code",
                    $"Hi {name},\n\nUse this code to reset your Aruga password:\n\n{code}\n\n" +
                    $"It expires in {CodeMinutes} minutes. If you didn't ask to reset your password, you can ignore this message; your password stays the same.",
                    highlight: code,
                    preview: "Open this email to see your password reset code.");

            if (!sent)
            {
                return StatusCode(502, new
                {
                    message = "The code couldn't be sent right now. Please try again, or ask the health center to reset your password."
                });
            }

            await _audit.LogAsync("Authentication", "Password Reset", $"Account – {account.Username}",
                $"Password reset code sent by {(bySms ? "SMS" : "email")}.",
                userId: account.ReferenceID, role: account.AccountType == "Parent" ? "Parent" : null);

            var masked = bySms ? MessageSender.MaskPhone(destination) : MessageSender.MaskEmail(destination);
            return Ok(new { message = $"We sent a 6-digit code to {masked}. It expires in {CodeMinutes} minutes." });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyDto dto)
        {
            var (ok, error, _, _) = await CheckCodeAsync(dto.Identifier, dto.Otp);
            if (!ok) return BadRequest(new { message = error });
            return Ok(new { message = "Code accepted." });
        }

        [HttpPost("reset")]
        public async Task<IActionResult> Reset([FromBody] ResetDto dto)
        {
            var complexity = AuthController.GetPasswordComplexityError(dto.NewPassword);
            if (complexity != null)
                return BadRequest(new { message = complexity });

            var (ok, error, account, otp) = await CheckCodeAsync(dto.Identifier, dto.Otp);
            if (!ok || account == null || otp == null)
                return BadRequest(new { message = error });

            if (!await _accounts.ChangePasswordAsync(account.AccountID, dto.NewPassword))
                return StatusCode(500, new { message = "Could not reset the password. Please try again." });

            // Use up the code and lift any login lock from earlier wrong tries
            otp.IsUsed = true;
            var tracked = await _context.Accounts.FirstAsync(a => a.AccountID == account.AccountID);
            tracked.FailedLoginAttempts = 0;
            tracked.LockedUntil = null;
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Authentication", "Password Reset", $"Account – {account.Username}",
                "Password was reset with an emailed/texted code.",
                userId: account.ReferenceID, role: account.AccountType == "Parent" ? "Parent" : null);

            // Heads-up to the owner, in case it wasn't them
            var target = await FindAccountAsync(dto.Identifier.Trim());
            if (target?.Email is { } email)
            {
                await _sender.SendEmailAsync(email, "Your password was changed",
                    $"Hi {target.Value.Name},\n\nThe password for your Aruga account ({account.Username}) was just reset. " +
                    "If this wasn't you, please contact Leveriza Health Center right away.");
            }

            return Ok(new { message = "Your password has been reset. You can now sign in." });
        }

        // Validates the newest unused code for this account. Counts wrong tries.
        private async Task<(bool Ok, string Error, Account? Account, AccountOtp? Otp)> CheckCodeAsync(string? identifier, string? code)
        {
            const string invalid = "That code is wrong or has expired. Please check it, or ask for a new one.";

            code = code?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(identifier) || code.Length != 6 || !code.All(char.IsDigit))
                return (false, "Enter the 6-digit code we sent you.", null, null);

            var target = await FindAccountAsync(identifier.Trim());
            if (target == null) return (false, invalid, null, null);
            var account = target.Value.Account;

            var otp = await _context.AccountOtps
                .Where(o => o.AccountID == account.AccountID && o.Purpose == Purpose && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null || otp.ExpiresAt < DateTime.UtcNow)
                return (false, invalid, null, null);

            if (otp.Attempts >= MaxAttempts)
                return (false, "Too many wrong tries. Please ask for a new code.", null, null);

            if (!BCrypt.Net.BCrypt.Verify(code, otp.OTPHash))
            {
                otp.Attempts++;
                await _context.SaveChangesAsync();
                int left = MaxAttempts - otp.Attempts;
                return (false, left > 0
                    ? $"That code is wrong. {left} {(left == 1 ? "try" : "tries")} left."
                    : "Too many wrong tries. Please ask for a new code.", null, null);
            }

            return (true, "", account, otp);
        }

        // Same lookup as login: Accounts.Username (a parent's email or a staff
        // username), then a parent's email, then a staff member's email.
        private async Task<(Account Account, string? Email, string? Phone, string Name)?> FindAccountAsync(string identifier)
        {
            var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Username == identifier);

            if (account == null)
            {
                var parentId = await _context.Parents.Where(p => p.Email == identifier).Select(p => (Guid?)p.ParentID).FirstOrDefaultAsync();
                if (parentId != null)
                    account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountType == "Parent" && a.ReferenceID == parentId);
            }
            if (account == null)
            {
                var userId = await _context.Users.Where(u => u.Email == identifier).Select(u => (Guid?)u.UserID).FirstOrDefaultAsync();
                if (userId != null)
                    account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountType == "Personnel" && a.ReferenceID == userId);
            }
            if (account == null) return null;

            if (account.AccountType == "Parent")
            {
                var p = await _context.Parents.AsNoTracking().FirstOrDefaultAsync(x => x.ParentID == account.ReferenceID);
                if (p == null) return null;
                return (account, p.Email, p.ContactNo, p.FirstName);
            }

            var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserID == account.ReferenceID);
            if (u == null) return null;
            return (account, u.Email, u.ContactNo, u.FirstName ?? "");
        }
    }
}
