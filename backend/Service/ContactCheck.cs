using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // One email address and one mobile number per person, across parents and
    // staff: sign-in, Forgot Password and reminders find people by them.
    // Returns a message for the form, or null when the value is free.
    public static class ContactCheck
    {
        public const string EmailTaken = "This email address is already registered.";
        public const string PhoneTaken = "This phone number is already registered.";

        // exceptParent / exceptUser: the person being edited (their own
        // email or number doesn't count as taken)
        public static async Task<string?> DuplicateAsync(
            AppDbContext context, string? email, string? phone,
            Guid? exceptParent = null, Guid? exceptUser = null)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                var e = email.Trim();
                bool taken =
                    await context.Parents.AnyAsync(p => p.Email == e && p.ParentID != exceptParent) ||
                    await context.Users.AnyAsync(u => u.Email == e && u.UserID != exceptUser) ||
                    await context.Accounts.AnyAsync(a => a.Username == e
                        && a.ReferenceID != (exceptParent ?? Guid.Empty) && a.ReferenceID != (exceptUser ?? Guid.Empty));
                if (taken) return EmailTaken;
            }

            var number = MessageSender.NormalizePhNumber(phone);
            if (number != null)
            {
                // Numbers are saved in different styles (0917…, +63 917 …),
                // so compare them the same way the SMS sender reads them
                var parentNumbers = await context.Parents
                    .Where(p => p.ContactNo != null && p.ParentID != exceptParent)
                    .Select(p => p.ContactNo).ToListAsync();
                var staffNumbers = await context.Users
                    .Where(u => u.ContactNo != null && u.UserID != exceptUser)
                    .Select(u => u.ContactNo).ToListAsync();
                if (parentNumbers.Concat(staffNumbers).Any(n => MessageSender.NormalizePhNumber(n) == number))
                    return PhoneTaken;
            }

            return null;
        }

        // Last line of defence: the database's own "already exists" error,
        // in words a clinic user understands
        public static string? FromDatabaseError(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                var m = e.Message;
                if (!m.Contains("UNIQUE KEY") && !m.Contains("duplicate key")) continue;
                if (m.Contains("Email") || m.Contains("UQ_Accounts_Username")) return EmailTaken;
                if (m.Contains("Username")) return "This username is already taken.";
                return "This record already exists.";
            }
            return null;
        }
    }
}
