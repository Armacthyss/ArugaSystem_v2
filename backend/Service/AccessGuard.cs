using System.Security.Claims;
using AndroidWebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AndroidWebAPI.Services
{
    // Role names in the sign-in token (see AuthController.Login).
    //
    // User levels:
    //   Super Admin    -> "SuperAdmin" (the development team / IT: audit logs
    //                     and the Admin accounts, never patient records)
    //   Admin / Doctor -> "SystemAdmin" (the doctor runs the clinic)
    //   Staff / Nurse  -> "Staff" (check-in, Call Next, vaccinating)
    //   Parent         -> "Parent"
    public static class Roles
    {
        public const string Parent = "Parent";
        public const string Staff = "Staff";             // Nurse
        public const string Admin = "SystemAdmin";       // Doctor
        public const string SuperAdmin = "SuperAdmin";   // the development team

        public const string StaffOrAdmin = Staff + "," + Admin;
        public const string ClinicTeam = StaffOrAdmin;
        public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;

        // Users.Position of a Super Admin (their UserType stays NULL)
        public const string SuperAdminPosition = "SuperAdmin";

        public static bool IsSuperAdminPosition(string? position) =>
            string.Equals(position, SuperAdminPosition, StringComparison.OrdinalIgnoreCase);

        // "Administrator" is the Admin position from before the revision
        public static bool IsDoctorPosition(string? position) =>
            string.Equals(position, "Doctor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(position, "Administrator", StringComparison.OrdinalIgnoreCase);

        // Users.Position -> portal. "Administrator" and "Staff" are positions
        // from before the revision; they keep working as Admin and Staff so
        // older accounts can still sign in.
        public static string ForPosition(string? position) =>
            IsSuperAdminPosition(position) ? SuperAdmin
            : IsDoctorPosition(position) ? Admin
            : Staff;

        // Positions each level may give a new account: the Doctor adds Nurses,
        // the Super Admin adds Doctors and other Super Admins.
        public static readonly string[] Positions = { "Nurse" };
        public static readonly string[] SuperAdminPositions = { "Doctor", SuperAdminPosition };
    }

    // Keeps each family's records private: a parent can only open their own
    // profile, children, notifications and queue ticket. Clinic personnel
    // (health workers, staff, admin) can see every patient. The Super Admin
    // manages the system, not patients (Data Privacy Act: only the personal
    // data a job needs), so family records stay closed to them.
    public static class AccessGuard
    {
        public static bool IsParent(ClaimsPrincipal user) => user.IsInRole(Roles.Parent);

        public static bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole(Roles.SuperAdmin);

        // Users.UserID for personnel, Parents.ParentID for parents
        public static Guid? CallerId(ClaimsPrincipal user) =>
            Guid.TryParse(user.FindFirst("ReferenceID")?.Value, out var id) ? id : null;

        public static bool CanSeeParent(ClaimsPrincipal user, Guid parentId) =>
            !IsSuperAdmin(user) && (!IsParent(user) || CallerId(user) == parentId);

        public static async Task<bool> CanSeeChildAsync(ClaimsPrincipal user, AppDbContext context, Guid childId)
        {
            if (IsSuperAdmin(user)) return false;
            if (!IsParent(user)) return true;
            var me = CallerId(user);
            return me != null && await context.ChildParentRelationships
                .AnyAsync(r => r.ChildID == childId && r.ParentID == me && r.Status == "Active");
        }
    }
}
