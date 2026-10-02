using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Models;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Data
{
    public class ChildParentRelationshipRepository
        : IChildParentRelationshipRepository
    {
        private readonly AppDbContext _context;

        public ChildParentRelationshipRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChildParentRelationship?> CreateAsync(
            CreateChildParentRelationshipDto dto)
        {
            // Verify child exists
            var childExists = await _context.Children
                .AnyAsync(c => c.ChildID == dto.ChildID);

            if (!childExists)
                throw new Exception("Child not found.");

            // Verify parent exists
            var parentExists = await _context.Parents
                .AnyAsync(p => p.ParentID == dto.ParentID);

            if (!parentExists)
                throw new Exception("Parent not found.");

            // Prevent duplicate relationship
            var existing = await _context.ChildParentRelationships
                .FirstOrDefaultAsync(r =>
                    r.ChildID == dto.ChildID &&
                    r.ParentID == dto.ParentID &&
                    r.Status == "Active");

            if (existing != null)
                throw new Exception(
                    "This parent is already linked to this child.");

            // A child has one Primary Contact, and when its reminders go to the
            // primary contact only, a newly linked guardian follows that setting
            var links = await _context.ChildParentRelationships
                .Where(r => r.ChildID == dto.ChildID && r.Status == "Active")
                .ToListAsync();
            bool primaryOnly = links.Any(r => !r.CanReceiveNotifications);
            if (dto.IsPrimaryContact)
            {
                foreach (var r in links.Where(r => r.IsPrimaryContact))
                {
                    r.IsPrimaryContact = false;
                    if (primaryOnly) r.CanReceiveNotifications = false;
                    r.UpdatedAt = DateTime.Now;
                }
            }

            var relationship = new ChildParentRelationship
            {
                RelationshipID = Guid.NewGuid(),
                ChildID = dto.ChildID,
                ParentID = dto.ParentID,
                RelationshipType = dto.RelationshipType,
                IsPrimaryContact = dto.IsPrimaryContact,
                CanReceiveNotifications = primaryOnly ? dto.IsPrimaryContact : dto.CanReceiveNotifications,
                Status = "Active",
                CreatedAt = DateTime.Now
            };

            _context.ChildParentRelationships.Add(relationship);

            await _context.SaveChangesAsync();

            return relationship;
        }

        public async Task<IEnumerable<ChildParentRelationship>>
            GetByChildAsync(Guid childID)
        {
            return await _context.ChildParentRelationships
                .Where(r =>
                    r.ChildID == childID &&
                    r.Status == "Active")
                .ToListAsync();
        }

        public async Task<IEnumerable<ChildParentRelationship>>
            GetByParentAsync(Guid parentID)
        {
            return await _context.ChildParentRelationships
                .Where(r =>
                    r.ParentID == parentID &&
                    r.Status == "Active")
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(Guid relationshipID)
        {
            var relationship =
                await _context.ChildParentRelationships
                    .FirstOrDefaultAsync(r =>
                        r.RelationshipID == relationshipID);

            if (relationship == null)
                return false;

            relationship.Status = "Inactive";
            relationship.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}