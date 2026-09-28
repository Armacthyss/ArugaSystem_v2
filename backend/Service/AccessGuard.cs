using System.Security.Claims;
using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Role names in the sign-in token (see AuthController.Login).
    //
    // Leveriza has three user levels (beneficiary revision, Sep 2026):
    //   Admin / Doctor -> "SystemAdmin" (the doctor is the highest authority)
    //   Staff / Nurse  -> "Staff" (check-in, Call Next, vaccinating)
    //   Parent         -> "Parent"
    public static class Roles
    {
        public const string Parent = "Parent";
        public const string Staff = "Staff";             // Nurse
        public const string Admin = "SystemAdmin";       // Doctor

        public const string StaffOrAdmin = Staff + "," + Admin;
        public const string ClinicTeam = StaffOrAdmin;

        // Users.Position -> portal. "Administrator" and "Staff" are positions
        // from before the revision; they keep working as Admin and Staff so
        // older accounts can still sign in.
        public static string ForPosition(string? position) =>
            string.Equals(position, "Doctor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(position, "Administrator", StringComparison.OrdinalIgnoreCase)
                ? Admin
                : Staff;

        // Positions the administrator can give a new account
        public static readonly string[] Positions = { "Doctor", "Nurse" };
    }

    // Keeps each family's records private: a parent can only open their own
    // profile, children, notifications and queue ticket. Clinic personnel
    // (health workers, staff, admin) can see every patient.
    public static class AccessGuard
    {
        public static bool IsParent(ClaimsPrincipal user) => user.IsInRole(Roles.Parent);

        // Users.UserID for personnel, Parents.ParentID for parents
        public static Guid? CallerId(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst("ReferenceID")?.Value, out var id) ? id : null;

        public static bool CanSeeParent(ClaimsPrincipal user, Guid parentId) =>
            !IsParent(user) || CallerId(user) == parentId;

        public static async Task<bool> CanSeeChildAsync(ClaimsPrincipal user, AppDbContext context, Guid childId)
        {
            if (!IsParent(user)) return true;
            var me = CallerId(user);
            return me != null && await context.ChildParentRelationships
                .AnyAsync(r => r.ChildID == childId && r.ParentID == me && r.Status == "Active");
        }
    }
}
