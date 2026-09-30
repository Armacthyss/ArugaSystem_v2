using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Data;
using AndroidWebAPI.DTOs;
using AndroidWebAPI.Services;

namespace AndroidWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChildParentRelationshipsController : ControllerBase
    {
        private readonly IChildParentRelationshipRepository _repository;
        private readonly AppDbContext _context;
        private readonly AuditService _audit;

        public ChildParentRelationshipsController(
            IChildParentRelationshipRepository repository,
            AppDbContext context,
            AuditService audit)
        {
            _repository = repository;
            _context = context;
            _audit = audit;
        }

        // POST /api/ChildParentRelationships
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateChildParentRelationshipDto dto)
        {
            if (dto.ChildID == Guid.Empty)
                return BadRequest(new
                {
                    message = "ChildID is required."
                });

            if (dto.ParentID == Guid.Empty)
                return BadRequest(new
                {
                    message = "ParentID is required."
                });

            if (string.IsNullOrWhiteSpace(dto.RelationshipType))
                return BadRequest(new
                {
                    message = "RelationshipType is required."
                });

            try
            {
                var relationship =
                    await _repository.CreateAsync(dto);

                return CreatedAtAction(
                    nameof(GetByChild),
                    new { childID = dto.ChildID },
                    new
                    {
                        relationshipID =
                            relationship!.RelationshipID,

                        childID =
                            relationship.ChildID,

                        parentID =
                            relationship.ParentID,

                        relationshipType =
                            relationship.RelationshipType,

                        isPrimaryContact =
                            relationship.IsPrimaryContact,

                        canReceiveNotifications =
                            relationship.CanReceiveNotifications,

                        status =
                            relationship.Status,

                        createdAt =
                            relationship.CreatedAt
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // GET /api/ChildParentRelationships/child/{childID}
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("child/{childID}")]
        public async Task<IActionResult> GetByChild(Guid childID)
        {
            try
            {
                var relationships =
                    await _repository.GetByChildAsync(childID);

                var result = relationships.Select(r => new
                {
                    relationshipID = r.RelationshipID,
                    childID = r.ChildID,
                    parentID = r.ParentID,
                    relationshipType = r.RelationshipType,
                    isPrimaryContact = r.IsPrimaryContact,
                    canReceiveNotifications =
                        r.CanReceiveNotifications,
                    status = r.Status,
                    createdAt = r.CreatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message
                });
            }
        }

        // GET /api/ChildParentRelationships/parent/{parentID}
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.ClinicTeam)]
        [HttpGet("parent/{parentID}")]
        public async Task<IActionResult> GetByParent(Guid parentID)
        {
            try
            {
                var relationships =
                    await _repository.GetByParentAsync(parentID);

                var result = relationships.Select(r => new
                {
                    relationshipID = r.RelationshipID,
                    childID = r.ChildID,
                    parentID = r.ParentID,
                    relationshipType = r.RelationshipType,
                    isPrimaryContact = r.IsPrimaryContact,
                    canReceiveNotifications =
                        r.CanReceiveNotifications,
                    status = r.Status,
                    createdAt = r.CreatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message
                });
            }
        }

        // DELETE /api/ChildParentRelationships/{relationshipID}
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = AndroidWebAPI.Services.Roles.StaffOrAdmin)]
        [HttpDelete("{relationshipID}")]
        public async Task<IActionResult> Delete(
            Guid relationshipID)
        {
            try
            {
                var deleted =
                    await _repository.DeleteAsync(relationshipID);

                if (!deleted)
                    return NotFound(new
                    {
                        message = "Relationship not found."
                    });

                return Ok(new
                {
                    message =
                        "Parent-child relationship removed successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = ex.Message
                });
            }
        }

        // PATCH /api/ChildParentRelationships/{relationshipID}/primary
        // Makes this guardian the child's one Primary Contact. When reminders
        // go to the primary contact only, they follow the new primary.
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.StaffOrAdmin)]
        [HttpPatch("{relationshipID}/primary")]
        public async Task<IActionResult> MakePrimary(Guid relationshipID)
        {
            var target = await _context.ChildParentRelationships
                .FirstOrDefaultAsync(r => r.RelationshipID == relationshipID && r.Status == "Active");
            if (target == null)
                return NotFound(new { message = "Relationship not found." });

            var links = await _context.ChildParentRelationships
                .Include(r => r.Parent)
                .Include(r => r.Child)
                .Where(r => r.ChildID == target.ChildID && r.Status == "Active")
                .ToListAsync();
            bool primaryOnly = links.Any(r => !r.CanReceiveNotifications);

            foreach (var r in links)
            {
                r.IsPrimaryContact = r.RelationshipID == relationshipID;
                if (primaryOnly) r.CanReceiveNotifications = r.IsPrimaryContact;
                r.UpdatedAt = DateTime.Now;
            }
            await _context.SaveChangesAsync();

            var parent = target.Parent;
            await _audit.LogAsync("Patient Management", "Update",
                $"Child – {target.Child?.FirstName} {target.Child?.LastName}",
                $"{parent?.FirstName} {parent?.LastName} ({target.RelationshipType}) is now the primary contact.");

            return Ok(new { message = "Primary contact updated." });
        }

        // PATCH /api/ChildParentRelationships/child/{childID}/notifications
        // Who gets this child's vaccination reminders: "primary" (the primary
        // contact only) or "all" (every linked parent/guardian).
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.StaffOrAdmin)]
        [HttpPatch("child/{childID}/notifications")]
        public async Task<IActionResult> SetNotifyMode(Guid childID, [FromBody] NotifyModeDto dto)
        {
            var mode = dto?.Mode?.Trim().ToLowerInvariant();
            if (mode != "primary" && mode != "all")
                return BadRequest(new { message = "Mode must be \"primary\" or \"all\"." });

            var links = await _context.ChildParentRelationships
                .Include(r => r.Child)
                .Where(r => r.ChildID == childID && r.Status == "Active")
                .ToListAsync();
            if (links.Count == 0)
                return NotFound(new { message = "This child has no linked parent or guardian." });
            if (mode == "primary" && !links.Any(r => r.IsPrimaryContact))
                return BadRequest(new { message = "Choose a Primary Contact first." });

            foreach (var r in links)
            {
                r.CanReceiveNotifications = mode == "all" || r.IsPrimaryContact;
                r.UpdatedAt = DateTime.Now;
            }
            await _context.SaveChangesAsync();

            var child = links[0].Child;
            await _audit.LogAsync("Patient Management", "Update",
                $"Child – {child?.FirstName} {child?.LastName}",
                mode == "all" ? "Reminders go to all linked accounts." : "Reminders go to the primary contact only.");

            return Ok(new { message = "Reminder recipients updated." });
        }
    }

    public class NotifyModeDto
    {
        public string? Mode { get; set; }
    }
}