using System.ComponentModel.DataAnnotations;

namespace AndroidWebAPI.Models
{
   public class Parent
{
    [Key]
    public Guid ParentID { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    // Nullable: a contact-only guardian (no portal login) can be
    // registered without an email.
    public string? Email { get; set; }

    public string ContactNo { get; set; } = string.Empty;

    public string? BarangayNo { get; set; }
    public string? Address { get; set; }

    public string? PasswordHash { get; set; }

    public bool MustChangePassword { get; set; } = false;

    public DateTime? TemporaryPasswordExpiresAt { get; set; }

    public DateTime? LastLogin { get; set; }

    // Data Privacy Act (RA 10173) consent:
    //  ConsentRecordedAt - staff ticked "the parent/guardian agreed" when
    //                      registering them at the health center
    //  PrivacyConsentAt  - the parent accepted the Data Privacy Notice
    //                      themselves in the app (asked at first sign-in)
    public DateTime? ConsentRecordedAt { get; set; }
    public DateTime? PrivacyConsentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ChildParentRelationship> ChildRelationships { get; set; }
        = new List<ChildParentRelationship>();
}
}