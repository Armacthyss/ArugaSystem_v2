using AndroidWebAPI.Models;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Dapper;

namespace AndroidWebAPI.Data
{
    public class ParentRepository(IConfiguration configuration)
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection");

        // ── Login ─────────────────────────────────────────────────

        public async Task<Parent?> LoginAsync(string email, string password)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string query = @"
        SELECT
            ParentID,
            FirstName,
            MiddleName,
            LastName,
            Email,
            PasswordHash,
            ContactNo,
            BarangayNo,
            Address,
            MustChangePassword,
            TemporaryPasswordExpiresAt,
            LastLogin
        FROM dbo.Parents
        WHERE Email = @Email";

    var parent = await connection.QueryFirstOrDefaultAsync<Parent>(
        query,
        new { Email = email }
    );

    if (parent == null)
        return null;

    if (string.IsNullOrEmpty(parent.PasswordHash))
        return null;

    bool passwordValid = BCrypt.Net.BCrypt.Verify(
        password,
        parent.PasswordHash
    );

    if (!passwordValid)
        return null;
        if (parent.MustChangePassword &&
    parent.TemporaryPasswordExpiresAt.HasValue &&
    parent.TemporaryPasswordExpiresAt.Value < DateTime.Now)
{
    return null;
}

    // Record successful login
    await connection.ExecuteAsync(
        @"
        UPDATE dbo.Parents
        SET LastLogin = GETDATE()
        WHERE ParentID = @ParentID",
        new { parent.ParentID }
    );

    parent.LastLogin = DateTime.Now;


    return parent;
}
   
        // ── Change Password ───────────────────────────────────────

        public async Task<(bool Success, string Message)> ChangePasswordAsync(
    Guid parentId,
    string currentPassword,
    string newPassword)
{
    using IDbConnection connection =
        new SqlConnection(_connectionString);

    var parent = await connection.QueryFirstOrDefaultAsync<Parent>(
        @"
        SELECT
            ParentID,
            PasswordHash,
            MustChangePassword,
            TemporaryPasswordExpiresAt
        FROM dbo.Parents
        WHERE ParentID = @ParentID
        ",
        new { ParentID = parentId }
    );

    if (parent == null)
        return (false, "Parent account not found.");

    if (string.IsNullOrWhiteSpace(parent.PasswordHash))
        return (false, "Account does not have a valid password.");

    // Check temporary password expiration
    if (parent.MustChangePassword &&
        parent.TemporaryPasswordExpiresAt.HasValue &&
        parent.TemporaryPasswordExpiresAt.Value < DateTime.Now)
    {
        return (false, "Temporary password has expired. Please request a new password.");
    }

    // Verify current password
    bool currentPasswordValid =
        BCrypt.Net.BCrypt.Verify(
            currentPassword,
            parent.PasswordHash
        );

    if (!currentPasswordValid)
        return (false, "Current password is incorrect.");

    // Basic new password validation
    if (string.IsNullOrWhiteSpace(newPassword))
        return (false, "New password is required.");

    if (newPassword.Length < 8)
        return (false, "New password must be at least 8 characters.");

    // Prevent using the same password
    if (BCrypt.Net.BCrypt.Verify(newPassword, parent.PasswordHash))
        return (false, "New password must be different from your current password.");

    string newPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(newPassword);

    int rows = await connection.ExecuteAsync(
        @"
        UPDATE dbo.Parents
        SET
            PasswordHash = @PasswordHash,
            MustChangePassword = 0,
            TemporaryPasswordExpiresAt = NULL,
            UpdatedAt = GETDATE()
        WHERE ParentID = @ParentID
        ",
        new
        {
            PasswordHash = newPasswordHash,
            ParentID = parentId
        }
    );

    if (rows == 0)
        return (false, "Failed to update password.");

    return (true, "Password updated successfully.");
}


        // ── Dashboard ─────────────────────────────────────────────
       


       public async Task<dynamic> GetDashboardData(Guid parentId)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
        SELECT
            p.ParentID,
            p.FirstName + ' ' + p.LastName AS ParentFullName,

            c.ChildID,
            c.FirstName,
            c.MiddleName,
            c.LastName,
            c.BirthDate,
            c.PlaceOfBirth,
            c.Sex,
            c.Barangay,
            c.FamilyNo,
            c.Address,
            c.HealthCenter,
            c.BirthWeight,
            c.BirthHeight,

            cpr.RelationshipType,
            cpr.IsPrimaryContact,
            cpr.CanReceiveNotifications,

            -- Everyone linked to the child: 'Maria Santos (Mother); Rosario Santos (Grandmother)'
            (SELECT STRING_AGG(gp.FirstName + ' ' + gp.LastName + ' (' + g.RelationshipType + ')', '; ')
                    WITHIN GROUP (ORDER BY g.IsPrimaryContact DESC, gp.FirstName)
             FROM dbo.ChildParentRelationship g
             JOIN dbo.Parents gp ON gp.ParentID = g.ParentID
             WHERE g.ChildID = c.ChildID AND g.Status = 'Active') AS Guardians

        FROM dbo.Parents p

        INNER JOIN dbo.ChildParentRelationship cpr
            ON p.ParentID = cpr.ParentID

        INNER JOIN dbo.Children c
            ON cpr.ChildID = c.ChildID

        WHERE p.ParentID = @Id
          AND cpr.Status = 'Active';

    ";

    return await connection.QueryAsync<dynamic>(
        sql,
        new { Id = parentId }
    );
}
public async Task<Parent> CreateAsync(Parent parent)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
        INSERT INTO Parents
        (
            ParentID,
            FirstName,
            MiddleName,
            LastName,
            Email,
            ContactNo,
            BarangayNo,
            Address,
            PasswordHash,
            MustChangePassword,
            TemporaryPasswordExpiresAt,
            ConsentRecordedAt,
            CreatedAt,
            UpdatedAt
        )
        VALUES
        (
            @ParentID,
            @FirstName,
            @MiddleName,
            @LastName,
            @Email,
            @ContactNo,
            @BarangayNo,
            @Address,
            @PasswordHash,
            @MustChangePassword,
            @TemporaryPasswordExpiresAt,
            @ConsentRecordedAt,
            GETDATE(),
            GETDATE()
        )";

    await connection.ExecuteAsync(sql, parent);

    return parent;
}


public async Task<Parent?> GetByIdAsync(Guid id)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
    SELECT *
    FROM Parents
    WHERE ParentID = @Id";

    return await connection.QueryFirstOrDefaultAsync<Parent>(sql, new { Id = id });
}

public async Task<IEnumerable<Parent>> GetAllAsync()
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
    SELECT *
    FROM Parents
    ORDER BY LastName, FirstName";

    return await connection.QueryAsync<Parent>(sql);
}

public async Task<Parent> UpdateAsync(Parent parent)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
    UPDATE Parents
    SET
        FirstName = @FirstName,
        MiddleName = @MiddleName,
        LastName = @LastName,
        Email = @Email,
        ContactNo = @ContactNo,
        BarangayNo = @BarangayNo,
        Address = @Address,
        UpdatedAt = GETDATE()
    WHERE ParentID = @ParentID";

    await connection.ExecuteAsync(sql, parent);

    return parent;
}
public async Task<bool> DeleteAsync(Guid id)
{
    using IDbConnection connection = new SqlConnection(_connectionString);

    string sql = @"
    DELETE FROM Parents
    WHERE ParentID = @Id";

    int rows = await connection.ExecuteAsync(sql, new { Id = id });

    return rows > 0;
}
    }
}