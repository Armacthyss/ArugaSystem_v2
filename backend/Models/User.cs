// Models/User.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    [Table("Users")]  // maps to dbo.Users in your DB
    public class User
    {
        [Key]
        public Guid UserID { get; set; }

        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? ContactNo { get; set; }
        public string? Address { get; set; }

        // NULL for a Super Admin (the column's CHECK rule lists the clinic's
        // positions only); Position is what decides the portal.
        public string? UserType { get; set; }

public string Position { get; set; } = string.Empty;
        public string? PRCNo { get; set; }
        public string AccountStatus { get; set; } = "Active";
    }
}