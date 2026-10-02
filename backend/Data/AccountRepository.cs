using AndroidWebAPI.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;
using AndroidWebAPI.DTOs;

namespace AndroidWebAPI.Data
{
    public class AccountRepositoryImpl : IAccountRepository
    {
        private readonly string _connectionString;

        public AccountRepositoryImpl(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is not configured.");
        }

       public async Task<Account?> LoginAsync(
    string identifier,
    string password,
    string accountType)
{
    using IDbConnection connection =
        new SqlConnection(_connectionString);

    Account? account;

    // =====================================================
    // 1. PARENT LOGIN
    // Identifier = Parent Email
    // =====================================================

    if (accountType.Equals(
        "Parent",
        StringComparison.OrdinalIgnoreCase))
    {
        account = await connection.QueryFirstOrDefaultAsync<Account>(
            @"
            SELECT a.*
            FROM dbo.Accounts a
            INNER JOIN dbo.Parents p
                ON a.ReferenceID = p.ParentID
            WHERE p.Email = @Identifier
              AND a.AccountType = 'Parent'
            ",
            new
            {
                Identifier = identifier
            }
        );
    }

    // =====================================================
    // 2. CLINIC PERSONNEL LOGIN
    // Identifier = Username
    // =====================================================

    else if (accountType.Equals(
        "ClinicPersonnel",
        StringComparison.OrdinalIgnoreCase))
    {
        account = await connection.QueryFirstOrDefaultAsync<Account>(
            @"
            SELECT a.*
            FROM dbo.Accounts a
            WHERE a.Username = @Identifier
              AND a.AccountType = 'Personnel'
            ",
            new
            {
                Identifier = identifier
            }
        );
    }

    // =====================================================
    // 3. INVALID ACCOUNT TYPE
    // =====================================================

    else
    {
        return null;
    }

    // =====================================================
    // 4. ACCOUNT VALIDATION
    // =====================================================

    if (account == null)
        return null;

    if (!account.Status)
        return null;

    if (string.IsNullOrWhiteSpace(account.PasswordHash))
        return null;

    // =====================================================
    // 5. PASSWORD VALIDATION
    // =====================================================

    bool valid;

    try
    {
        valid = BCrypt.Net.BCrypt.Verify(
            password,
            account.PasswordHash
        );
    }
    catch
    {
        valid = false;
    }

    if (!valid)
        return null;

    return account;
}

        public async Task<Account?> GetByIdAsync(Guid accountId)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            return await connection.QueryFirstOrDefaultAsync<Account>(
                @"
                SELECT *
                FROM dbo.Accounts
                WHERE AccountID = @AccountID
                ",
                new { AccountID = accountId }
            );
        }

        public async Task<Account?> GetByEmailAsync(string email)
        {
            // Your current Accounts table does not have an Email column.
            // Email is stored in Parents/Personnel records for now.
            return null;
        }

        public async Task<Account?> GetByUsernameAsync(string username)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            return await connection.QueryFirstOrDefaultAsync<Account>(
                @"
                SELECT *
                FROM dbo.Accounts
                WHERE Username = @Username
                ",
                new { Username = username }
            );
        }

        public async Task<Account> CreateAsync(Account account)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            if (account.AccountID == Guid.Empty)
                account.AccountID = Guid.NewGuid();

            if (account.CreatedAt == default)
                account.CreatedAt = DateTime.Now;

            await connection.ExecuteAsync(
                @"
                INSERT INTO dbo.Accounts
                (
                    AccountID,
                    Username,
                    PasswordHash,
                    AccountType,
                    ReferenceID,
                    Status,
                    MustChangePassword,
                    FailedLoginAttempts,
                    LockedUntil,
                    LastLogin,
                    CreatedAt,
                    UpdatedAt
                )
                VALUES
                (
                    @AccountID,
                    @Username,
                    @PasswordHash,
                    @AccountType,
                    @ReferenceID,
                    @Status,
                    @MustChangePassword,
                    @FailedLoginAttempts,
                    @LockedUntil,
                    @LastLogin,
                    @CreatedAt,
                    @UpdatedAt
                )
                ",
                account
            );

            return account;
        }

        public async Task<Account> UpdateAsync(Account account)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            await connection.ExecuteAsync(
                @"
                UPDATE dbo.Accounts
                SET
                    Username = @Username,
                    PasswordHash = @PasswordHash,
                    AccountType = @AccountType,
                    ReferenceID = @ReferenceID,
                    Status = @Status,
                    MustChangePassword = @MustChangePassword,
                    FailedLoginAttempts = @FailedLoginAttempts,
                    LockedUntil = @LockedUntil,
                    LastLogin = @LastLogin,
                    UpdatedAt = @Now
                WHERE AccountID = @AccountID
                ",
                WithNow(account)
            );

            return account;
        }
public async Task<bool> ChangePasswordAsync(
    Guid accountId,
    string newPassword)
{
    using IDbConnection connection =
        new SqlConnection(_connectionString);

    string passwordHash =
        BCrypt.Net.BCrypt.HashPassword(newPassword);

    int rows = await connection.ExecuteAsync(
        @"
        UPDATE dbo.Accounts
        SET
            PasswordHash = @PasswordHash,
            MustChangePassword = 0,
            UpdatedAt = @Now
        WHERE AccountID = @AccountID
        ",
        new
        {
            AccountID = accountId,
            PasswordHash = passwordHash,
            Now = DateTime.Now
        }
    );

    return rows > 0;
}



        public async Task<bool> UpdateLastLoginAsync(Guid accountId)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            int rows = await connection.ExecuteAsync(
                @"
                UPDATE dbo.Accounts
                SET LastLogin = @Now
                WHERE AccountID = @AccountID
                ",
                new { AccountID = accountId, Now = DateTime.Now }
            );

            return rows > 0;
        }

        // Times come from the API's clock (Manila), not the database server's,
        // which may run on UTC (e.g. SQL Server in Docker)
        private static DynamicParameters WithNow(object template)
        {
            var parameters = new DynamicParameters(template);
            parameters.Add("Now", DateTime.Now);
            return parameters;
        }

        public async Task<bool> DeleteAsync(Guid accountId)
        {
            using IDbConnection connection =
                new SqlConnection(_connectionString);

            int rows = await connection.ExecuteAsync(
                @"
                DELETE FROM dbo.Accounts
                WHERE AccountID = @AccountID
                ",
                new { AccountID = accountId }
            );

            return rows > 0;
        }
    }
}