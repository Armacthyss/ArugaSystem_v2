/* =====================================================================
   ARUGA — SUPER ADMIN ACCOUNTS (the development team)
   =====================================================================
   The Super Admin level (technical adviser, Oct 2026) is for the people
   who build and maintain Aruga. A Super Admin sees the audit logs and
   manages the Admin / Doctor accounts; they cannot open patient records.

     gabriel_barlam   Gabriel Ryan D. Barlam
     renzo_palmon     Renzo Miguel Palmon

   Starting password for both:  SuperAdmin@2026
   (each is asked for a new password at the first sign-in)

   Safe to run again: an account that already exists is left alone.
   Schema.sql runs the same step for a new database; for an existing
   ArugaSystemDB, open this file in SSMS and press Execute.
   ===================================================================== */

USE ArugaSystemDB;   -- if the database doesn't exist, nothing below runs

SET NOCOUNT ON;

-- BCrypt hash of  SuperAdmin@2026
DECLARE @SuperPwd nvarchar(100) = N'$2b$11$eTkLnq4/jtcQN3tPlIStNeYFOl8OfGn6Ch5TfQZ0lUn9kCeKn1.9S';

DECLARE @Super TABLE (UserID uniqueidentifier, AccountID uniqueidentifier, Username nvarchar(50),
                      FirstName nvarchar(50), MiddleName nvarchar(50), LastName nvarchar(50));
INSERT @Super VALUES
 ('5A000000-0000-0000-0000-000000000001', '5A000000-0000-0000-0001-000000000001', N'gabriel_barlam', N'Gabriel Ryan', N'D.', N'Barlam'),
 ('5A000000-0000-0000-0000-000000000002', '5A000000-0000-0000-0001-000000000002', N'renzo_palmon',   N'Renzo Miguel', NULL,   N'Palmon');

-- Position 'SuperAdmin' picks the portal; UserType stays NULL (its CHECK
-- rule only lists the clinic's positions)
INSERT dbo.Users (UserID, FirstName, MiddleName, LastName, Username, PasswordHash, UserType, Position, AccountStatus)
SELECT s.UserID, s.FirstName, s.MiddleName, s.LastName, s.Username, @SuperPwd, NULL, N'SuperAdmin', N'Active'
FROM @Super s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.UserID = s.UserID OR u.Username = s.Username);

INSERT dbo.Accounts (AccountID, Username, PasswordHash, AccountType, ReferenceID, Status, MustChangePassword, FailedLoginAttempts, CreatedAt)
SELECT s.AccountID, s.Username, @SuperPwd, 'Personnel', s.UserID, 1, 1, 0, GETDATE()
FROM @Super s
WHERE EXISTS (SELECT 1 FROM dbo.Users u WHERE u.UserID = s.UserID)
  AND NOT EXISTS (SELECT 1 FROM dbo.Accounts a WHERE a.AccountID = s.AccountID OR a.Username = s.Username);

PRINT 'Super Admin accounts ready: gabriel_barlam, renzo_palmon (password SuperAdmin@2026, change it at first sign-in).';
