using AndroidWebAPI.Models;

namespace AndroidWebAPI.Data

{
    public interface IChildrenRepository
    {
        // CRUD
        Task<IEnumerable<Child>> GetAllAsync();

        Task<Child?> GetByIdAsync(Guid childId);

        Task<Child> CreateChildAsync(CreateChildDto dto);

        Task<Child?> UpdateChildAsync(Guid childId, UpdateChildDto dto);

        Task DeleteAsync(Guid childId);

        // Queries
        Task<IEnumerable<Child>> GetByParentAsync(Guid parentId);

        // Business
        Task GenerateTimelineAsync(Guid childId);
    }

    // ── DTOs ──────────────────────────────────────────────────────
    // Moved from ChildrenController so the repository layer can accept
    // them directly without depending on the Controllers namespace.

    public class ParentLinkDto
    {
        public Guid ParentID { get; set; }
        public string? RelationshipType { get; set; } // e.g. "Mother", "Father", "Guardian"
        public bool IsPrimaryContact { get; set; }
        public bool CanReceiveNotifications { get; set; } = true;
    }

    public class CreateChildDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? Sex { get; set; }
        public int? Barangay { get; set; }
        public string? FamilyNo { get; set; }
        public string? Address { get; set; }
        public string? HealthCenter { get; set; }
        public string? Allergies { get; set; }
        public decimal? BirthHeight { get; set; }
        public decimal? BirthWeight { get; set; }

        // One or more parent links to create in ChildParentRelationship
        public List<ParentLinkDto> Parents { get; set; } = new();
    }

    public class UpdateChildDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string? PlaceOfBirth { get; set; }
        public string? Sex { get; set; }
        public int? Barangay { get; set; }
        public string? FamilyNo { get; set; }
        public string? Address { get; set; }
        public string? HealthCenter { get; set; }
        public string? Allergies { get; set; }
        public decimal? BirthHeight { get; set; }
        public decimal? BirthWeight { get; set; }

        // No relationship fields here — relationship updates are handled
        // by a separate endpoint/controller against ChildParentRelationship.
    }

    // ── Domain Exception ─────────────────────────────────────────
    // Thrown by the repository when a referenced ParentID does not exist.
    // The controller catches this to return a 400 Bad Request, matching
    // the original inline-SQL behavior.
    public class ParentNotFoundException : Exception
    {
        public Guid ParentId { get; }

        public ParentNotFoundException(Guid parentId)
            : base($"Parent not found: {parentId}")
        {
            ParentId = parentId;
        }
    }
}