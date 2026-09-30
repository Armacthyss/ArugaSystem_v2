using AndroidWebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Data
{
    public class ChildrenRepository : IChildrenRepository
    {
        private readonly AppDbContext _context;
        private readonly IVaccinationTimelineRepository _timelineRepository;

        public ChildrenRepository(
            AppDbContext context,
            IVaccinationTimelineRepository timelineRepository)
        {
            _context = context;
            _timelineRepository = timelineRepository;
        }

        public async Task<IEnumerable<Child>> GetAllAsync()
        {
            return await _context.Children
                .Include(c => c.ParentRelationships)
                    .ThenInclude(r => r.Parent)
                .ToListAsync();
        }

        public async Task<Child?> GetByIdAsync(Guid childId)
        {
            return await _context.Children
                .Include(c => c.ParentRelationships)
                    .ThenInclude(r => r.Parent)
                .FirstOrDefaultAsync(c => c.ChildID == childId);
        }

        // ── CREATE ───────────────────────────────────────────────
        // Replicates the original controller behavior:
        //   1. verify every referenced ParentID exists
        //   2. insert the Child
        //   3. insert one ChildParentRelationship per parent link
        //   4. generate the vaccination timeline
        public async Task<Child> CreateChildAsync(CreateChildDto dto)
        {
            foreach (var link in dto.Parents)
            {
                var parentExists = await _context.Parents
                    .AnyAsync(p => p.ParentID == link.ParentID);

                if (!parentExists)
                    throw new ParentNotFoundException(link.ParentID);
            }

            var child = new Child
            {
                ChildID = Guid.NewGuid(),
                FirstName = dto.FirstName,
                MiddleName = dto.MiddleName ?? string.Empty,
                LastName = dto.LastName,
                BirthDate = dto.BirthDate,
                PlaceOfBirth = dto.PlaceOfBirth ?? string.Empty,
                Sex = dto.Sex ?? string.Empty,
                Barangay = dto.Barangay,
                FamilyNo = string.IsNullOrWhiteSpace(dto.FamilyNo) ? null : dto.FamilyNo.Trim(),
                Address = dto.Address ?? string.Empty,
                HealthCenter = dto.HealthCenter ?? string.Empty,
                Allergies = dto.Allergies,
                BirthHeight = dto.BirthHeight,
                BirthWeight = dto.BirthWeight,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var link in dto.Parents)
            {
                child.ParentRelationships.Add(new ChildParentRelationship
                {
                    RelationshipID = Guid.NewGuid(),
                    ChildID = child.ChildID,
                    ParentID = link.ParentID,
                    RelationshipType = link.RelationshipType ?? "Parent",
                    IsPrimaryContact = link.IsPrimaryContact,
                    CanReceiveNotifications = link.CanReceiveNotifications,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Child + relationships are inserted in a single SaveChangesAsync
            // call, which EF Core wraps in one atomic transaction.
            await _context.Children.AddAsync(child);
            await _context.SaveChangesAsync();

            // Preserve existing behavior: auto-generate the vaccination
            // timeline immediately after a child is created.
            await _timelineRepository.GenerateTimelineAsync(child.ChildID);

            return child;
        }

        // ── UPDATE ───────────────────────────────────────────────
        // Only updates Children table columns, exactly like the original
        // endpoint. Relationship changes remain out of scope here.
        public async Task<Child?> UpdateChildAsync(Guid childId, UpdateChildDto dto)
        {
            var child = await _context.Children
                .FirstOrDefaultAsync(c => c.ChildID == childId);

            if (child == null)
                return null;

            bool birthDateChanged = child.BirthDate.Date != dto.BirthDate.Date;

            child.FirstName = dto.FirstName;
            child.MiddleName = dto.MiddleName ?? string.Empty;
            child.LastName = dto.LastName;
            child.BirthDate = dto.BirthDate;
            child.PlaceOfBirth = dto.PlaceOfBirth ?? string.Empty;
            child.Sex = dto.Sex ?? string.Empty;
            child.Barangay = dto.Barangay;
            child.FamilyNo = string.IsNullOrWhiteSpace(dto.FamilyNo) ? null : dto.FamilyNo.Trim();
            child.Address = dto.Address ?? string.Empty;
            child.HealthCenter = dto.HealthCenter ?? string.Empty;
            child.Allergies = dto.Allergies;
            child.BirthHeight = dto.BirthHeight;
            child.BirthWeight = dto.BirthWeight;
            child.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // A corrected birth date moves every dose that hasn't been given yet
            if (birthDateChanged)
                await _timelineRepository.RescheduleChildAsync(childId);

            return child;
        }

        public async Task DeleteAsync(Guid childId)
        {
            var child = await _context.Children.FindAsync(childId);

            if (child != null)
            {
                _context.Children.Remove(child);
                await _context.SaveChangesAsync();
            }
        }

        public async Task GenerateTimelineAsync(Guid childId)
        {
            await _timelineRepository.GenerateTimelineAsync(childId);
        }

        // ── READ: children linked to a given parent ────────────────
        public async Task<IEnumerable<Child>> GetByParentAsync(Guid parentId)
        {
            return await _context.Children
                .Include(c => c.ParentRelationships)
                .Where(c =>
                    c.ParentRelationships.Any(r =>
                        r.ParentID == parentId &&
                        r.Status == "Active"))
                .ToListAsync();
        }
    }
}