using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    // One-time codes sent to an account (Forgot Password). Only the BCrypt
    // hash of the code is stored, never the code itself.
    [Table("AccountOTPs")]
    public class AccountOtp
    {
        [Key]
        public Guid OTPID { get; set; }

        public Guid AccountID { get; set; }

        [Required]
        [MaxLength(255)]
        public string OTPHash { get; set; } = string.Empty;

        // "PasswordReset"
        [Required]
        [MaxLength(50)]
        public string Purpose { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; } = false;

        // Wrong guesses so far; the code stops working after 5.
        public int Attempts { get; set; } = 0;

        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(AccountID))]
        public virtual Account? Account { get; set; }
    }
}
