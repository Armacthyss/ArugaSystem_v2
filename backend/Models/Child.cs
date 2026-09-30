using System.ComponentModel.DataAnnotations;

namespace AndroidWebAPI.Models
{
    public class Child
    {
        [Key]
        public Guid ChildID { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        public string LastName { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; }

        public string? PlaceOfBirth { get; set; }

        public string? Address { get; set; }

        public string? HealthCenter { get; set; }
        public int? Barangay { get; set; }

        // Family (household) number the health center files the family under
        public string? FamilyNo { get; set; }

        public string? Sex { get; set; }
        public string? Allergies { get; set; }

        // e.g. asthma, heart condition. Doctors/Nurses may update this and
        // Allergies (PATCH /api/Children/{id}/health-notes).
        public string? ExistingConditions { get; set; }

        // Birth measurements, taken at registration (or added later via Edit).
        // decimal(5,2) matches sensible bounds for both fields: heights up to
        // 999.99 cm and weights up to 999.99 kg comfortably cover any real
        // newborn/child value with 2 decimal places of precision.
        public decimal? BirthHeight { get; set; }
        public decimal? BirthWeight { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Property
        public virtual ICollection<ChildParentRelationship> ParentRelationships { get; set; }
            = new List<ChildParentRelationship>();
    }
}