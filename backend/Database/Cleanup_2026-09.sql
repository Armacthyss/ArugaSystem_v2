/* =====================================================================
   ARUGA — DATABASE CLEANUP  (September 2026)
   =====================================================================
   Run this ONCE on an existing ArugaSystemDB to bring it in line with
   the current system. It is safe to run again: every step checks first.

   BACK UP FIRST. In SSMS: right-click ArugaSystemDB > Tasks > Back Up...

   What it changes, in plain words:

    1. Removes old tables nothing uses any more
         Queue ............... replaced by Queues + QueueChildren
         Personnel, Staff .... replaced by Users (+ Accounts for logins)
         PasswordOtps,
         PasswordResetTokens . replaced by AccountOTPs (Forgot Password)
    2. Removes copies of the same rule that were created every time an
       old setup script was re-run (3x the same foreign key, 3x the same
       "unique email" rule, ...). One of each is kept.
    3. Gives the remaining keys and rules readable names
       (PK__Children__BEFA0736... becomes PK_Children).
    4. Removes the Doctor Diagnosis columns. Remarks (NurseObservation)
       replaced them. Any diagnosis text that exists is first copied into
       the Remarks so nothing is lost.
    5. Creates the two tables QR check-in needs (QueueQRSettings,
       QueueQRCodes). QR check-in starts switched ON.
    6. Prepares AccountOTPs for the Forgot Password codes.
    7. Adds VaccineInventory.ManufacturingDate (from the documentation).
       Adds Children.FamilyNo (the family number the clinic files by)
       and Children.ExistingConditions (e.g. asthma).
    8. Lets Users.UserType say "Administrator" instead of "Admission"
       for admins.
    9. Adds indexes for the lookups the system does most often.
   10. Frees stations still marked busy from an earlier day, and keeps
       each health worker on one station only. The vaccination rooms are
       named Room 1, Room 2, Room 3; the test stations made during
       development (101, 102, 103, Station A/B/C) are removed.

   Open in SSMS and press Execute: it switches to ArugaSystemDB by itself
   (or: sqlcmd -S <server> -E -I -i Cleanup_2026-09.sql)
   ===================================================================== */

USE ArugaSystemDB;   -- if the database doesn't exist, nothing below runs

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @sql nvarchar(max);

/* ---------------------------------------------------------------------
   1. OLD TABLES
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[Queue]', N'U') IS NOT NULL             DROP TABLE dbo.[Queue];
IF OBJECT_ID(N'dbo.Personnel', N'U') IS NOT NULL           DROP TABLE dbo.Personnel;
IF OBJECT_ID(N'dbo.Staff', N'U') IS NOT NULL               DROP TABLE dbo.Staff;
IF OBJECT_ID(N'dbo.PasswordOtps', N'U') IS NOT NULL        DROP TABLE dbo.PasswordOtps;
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NOT NULL DROP TABLE dbo.PasswordResetTokens;
IF OBJECT_ID(N'dbo.Parent_SaveParent', N'P') IS NOT NULL   DROP PROCEDURE dbo.Parent_SaveParent;

/* ---------------------------------------------------------------------
   2. DUPLICATE RULES
   --------------------------------------------------------------------- */

-- Foreign keys: same column -> same table more than once. Keep the one
-- with a hand-written name if there is one.
SET @sql = N'';
WITH fk AS (
    SELECT f.name, f.parent_object_id,
           ROW_NUMBER() OVER (
               PARTITION BY f.parent_object_id, fc.parent_column_id, f.referenced_object_id
               ORDER BY CASE WHEN f.name LIKE N'FK[_][_]%' THEN 1 ELSE 0 END, f.name) AS rn
    FROM sys.foreign_keys f
    JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
)
SELECT @sql += N'ALTER TABLE dbo.' + QUOTENAME(OBJECT_NAME(parent_object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + NCHAR(10)
FROM fk WHERE rn > 1;
EXEC sp_executesql @sql;

-- Unique rules on the same column(s) more than once (Parents.Email,
-- Users.Username).
SET @sql = N'';
WITH uqc AS (
    SELECT i.name, i.object_id,
           (SELECT COUNT(*)       FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) AS ncols,
           (SELECT MIN(column_id) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) AS c1,
           (SELECT MAX(column_id) FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id) AS c2
    FROM sys.indexes i
    WHERE i.is_unique_constraint = 1
), uq AS (
    SELECT name, object_id,
           ROW_NUMBER() OVER (
               PARTITION BY object_id, ncols, c1, c2
               ORDER BY CASE WHEN name LIKE N'UQ[_][_]%' THEN 1 ELSE 0 END, name) AS rn
    FROM uqc
)
SELECT @sql += N'ALTER TABLE dbo.' + QUOTENAME(OBJECT_NAME(object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' + NCHAR(10)
FROM uq WHERE rn > 1;
EXEC sp_executesql @sql;

-- Users.UserType had the same CHECK three times. Replace them all with
-- one that also allows "Administrator".
SET @sql = N'';
SELECT @sql += N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + NCHAR(10)
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Users') AND cc.definition LIKE N'%UserType%';
EXEC sp_executesql @sql;

UPDATE dbo.Users SET UserType = N'Administrator' WHERE Position = N'Administrator';
UPDATE dbo.Users SET UserType = N'Admission'     WHERE Position = N'Staff' AND ISNULL(UserType, N'') <> N'Admission';

ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_UserType
    CHECK (UserType IN (N'Doctor', N'Nurse', N'Admission', N'Administrator'));

/* ---------------------------------------------------------------------
   4. DOCTOR DIAGNOSIS -> REMARKS
   (runs before the renaming so its foreign key is still easy to find)
   --------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.VaccinationRecords', N'DoctorDiagnosis') IS NOT NULL
BEGIN
    EXEC (N'
        UPDATE dbo.VaccinationRecords
        SET NurseObservation = CASE
                WHEN NULLIF(LTRIM(RTRIM(NurseObservation)), N'''') IS NULL THEN DoctorDiagnosis
                ELSE NurseObservation + NCHAR(10) + N''Doctor: '' + DoctorDiagnosis END
        WHERE NULLIF(LTRIM(RTRIM(DoctorDiagnosis)), N'''') IS NOT NULL;');

    SET @sql = N'';
    SELECT @sql += N'ALTER TABLE dbo.VaccinationRecords DROP CONSTRAINT ' + QUOTENAME(f.name) + N';' + NCHAR(10)
    FROM sys.foreign_keys f
    JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
    WHERE f.parent_object_id = OBJECT_ID(N'dbo.VaccinationRecords')
      AND COL_NAME(fc.parent_object_id, fc.parent_column_id) = N'DoctorDiagnosedByUserID';
    EXEC sp_executesql @sql;

    ALTER TABLE dbo.VaccinationRecords DROP COLUMN DoctorDiagnosis;
END
IF COL_LENGTH(N'dbo.VaccinationRecords', N'DoctorDiagnosedByUserID') IS NOT NULL
    ALTER TABLE dbo.VaccinationRecords DROP COLUMN DoctorDiagnosedByUserID;
IF COL_LENGTH(N'dbo.VaccinationRecords', N'DoctorDiagnosedAt') IS NOT NULL
    ALTER TABLE dbo.VaccinationRecords DROP COLUMN DoctorDiagnosedAt;

/* ---------------------------------------------------------------------
   5. QR CHECK-IN TABLES
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.QueueQRSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QueueQRSettings (
        SettingID  int       IDENTITY(1,1) NOT NULL CONSTRAINT PK_QueueQRSettings PRIMARY KEY,
        IsEnabled  bit       NOT NULL CONSTRAINT DF_QueueQRSettings_IsEnabled DEFAULT (1),
        UpdatedAt  datetime2 NOT NULL CONSTRAINT DF_QueueQRSettings_UpdatedAt DEFAULT (SYSDATETIME())
    );
    INSERT dbo.QueueQRSettings (IsEnabled) VALUES (1);
END

IF OBJECT_ID(N'dbo.QueueQRCodes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QueueQRCodes (
        QRCodeID   uniqueidentifier NOT NULL CONSTRAINT PK_QueueQRCodes PRIMARY KEY
                                             CONSTRAINT DF_QueueQRCodes_QRCodeID DEFAULT (NEWID()),
        Token      nvarchar(64)     NOT NULL,
        ShortCode  nvarchar(10)     NOT NULL,   -- typed in by hand when the camera can't scan
        QRDate     date             NOT NULL,
        ValidFrom  datetime2        NOT NULL,
        ValidUntil datetime2        NOT NULL,
        IsActive   bit              NOT NULL CONSTRAINT DF_QueueQRCodes_IsActive DEFAULT (1),
        CreatedAt  datetime2        NOT NULL CONSTRAINT DF_QueueQRCodes_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT UQ_QueueQRCodes_Token UNIQUE (Token)
    );
    CREATE INDEX IX_QueueQRCodes_QRDate ON dbo.QueueQRCodes (QRDate);
END

/* ---------------------------------------------------------------------
   6. FORGOT PASSWORD CODES (AccountOTPs)
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AccountOTPs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountOTPs (
        OTPID     uniqueidentifier NOT NULL CONSTRAINT PK_AccountOTPs PRIMARY KEY,
        AccountID uniqueidentifier NOT NULL,
        OTPHash   nvarchar(255)    NOT NULL,
        Purpose   nvarchar(50)     NOT NULL,
        ExpiresAt datetime2        NOT NULL,
        IsUsed    bit              NOT NULL CONSTRAINT DF_AccountOTPs_IsUsed DEFAULT (0),
        Attempts  int              NOT NULL CONSTRAINT DF_AccountOTPs_Attempts DEFAULT (0),
        CreatedAt datetime2        NOT NULL CONSTRAINT DF_AccountOTPs_CreatedAt DEFAULT (GETUTCDATE())
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.AccountOTPs')
                 AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.AccountOTPs'), N'OTPID', 'ColumnId'))
    ALTER TABLE dbo.AccountOTPs ADD CONSTRAINT DF_AccountOTPs_OTPID DEFAULT (NEWID()) FOR OTPID;

-- Codes go with their account: deleting an account deletes its codes.
SET @sql = N'';
SELECT @sql += N'ALTER TABLE dbo.AccountOTPs DROP CONSTRAINT ' + QUOTENAME(name) + N';' + NCHAR(10)
FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'dbo.AccountOTPs') AND delete_referential_action = 0;
EXEC sp_executesql @sql;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.AccountOTPs'))
    ALTER TABLE dbo.AccountOTPs ADD CONSTRAINT FK_AccountOTPs_Accounts
        FOREIGN KEY (AccountID) REFERENCES dbo.Accounts (AccountID) ON DELETE CASCADE;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccountOTPs_AccountID')
    CREATE INDEX IX_AccountOTPs_AccountID ON dbo.AccountOTPs (AccountID, Purpose);

/* ---------------------------------------------------------------------
   7. VACCINE MANUFACTURING DATE
   --------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.VaccineInventory', N'ManufacturingDate') IS NULL
    ALTER TABLE dbo.VaccineInventory ADD ManufacturingDate date NULL;

-- Family (household) number: the health center identifies families by it
IF COL_LENGTH(N'dbo.Children', N'FamilyNo') IS NULL
    ALTER TABLE dbo.Children ADD FamilyNo nvarchar(20) NULL;

-- Existing conditions (e.g. asthma), kept next to Allergies
IF COL_LENGTH(N'dbo.Children', N'ExistingConditions') IS NULL
    ALTER TABLE dbo.Children ADD ExistingConditions nvarchar(500) NULL;

/* ---------------------------------------------------------------------
   3. READABLE NAMES for keys and rules that SQL Server named itself
      (PK__Children__BEFA0736...  ->  PK_Children)
   --------------------------------------------------------------------- */
DECLARE @old sysname, @new sysname;
DECLARE names CURSOR LOCAL FAST_FORWARD FOR
    -- primary keys
    SELECT kc.name, N'PK_' + OBJECT_NAME(kc.parent_object_id)
    FROM sys.key_constraints kc
    WHERE kc.type = 'PK' AND kc.name LIKE N'PK[_][_]%'
    UNION ALL
    -- unique rules (single column)
    SELECT kc.name, N'UQ_' + OBJECT_NAME(kc.parent_object_id) + N'_' + COL_NAME(ic.object_id, ic.column_id)
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id AND ic.key_ordinal = 1
    WHERE kc.type = 'UQ' AND kc.name LIKE N'UQ[_][_]%'
    UNION ALL
    -- foreign keys
    SELECT f.name, N'FK_' + OBJECT_NAME(f.parent_object_id) + N'_' + COL_NAME(fc.parent_object_id, fc.parent_column_id)
    FROM sys.foreign_keys f
    JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
    WHERE f.name LIKE N'FK[_][_]%'
    UNION ALL
    -- defaults
    SELECT dc.name, N'DF_' + OBJECT_NAME(dc.parent_object_id) + N'_' + COL_NAME(dc.parent_object_id, dc.parent_column_id)
    FROM sys.default_constraints dc
    WHERE dc.name LIKE N'DF[_][_]%';
OPEN names;
FETCH NEXT FROM names INTO @old, @new;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@new)) IS NULL
    BEGIN
        SET @old = N'dbo.' + QUOTENAME(@old);
        EXEC sp_rename @old, @new, N'OBJECT';
    END
    FETCH NEXT FROM names INTO @old, @new;
END
CLOSE names;
DEALLOCATE names;

/* ---------------------------------------------------------------------
   9. INDEXES for the most common lookups
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Queues_QueueDate')
    CREATE INDEX IX_Queues_QueueDate ON dbo.Queues (QueueDate, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueChildren_QueueID')
    CREATE INDEX IX_QueueChildren_QueueID ON dbo.QueueChildren (QueueID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueChildren_ChildID')
    CREATE INDEX IX_QueueChildren_ChildID ON dbo.QueueChildren (ChildID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChildParentRelationship_ParentID')
    CREATE INDEX IX_ChildParentRelationship_ParentID ON dbo.ChildParentRelationship (ParentID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChildParentRelationship_ChildID')
    CREATE INDEX IX_ChildParentRelationship_ChildID ON dbo.ChildParentRelationship (ChildID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_ParentID')
    CREATE INDEX IX_Notifications_ParentID ON dbo.Notifications (ParentID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_UserID')
    CREATE INDEX IX_Notifications_UserID ON dbo.Notifications (UserID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_ActionDate')
    CREATE INDEX IX_AuditLogs_ActionDate ON dbo.AuditLogs (ActionDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccinationRecords_VaccinationDate')
    CREATE INDEX IX_VaccinationRecords_VaccinationDate ON dbo.VaccinationRecords (VaccinationDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccinationTimeline_ScheduledDate')
    CREATE INDEX IX_VaccinationTimeline_ScheduledDate ON dbo.VaccinationTimeline (ScheduledDate, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccineInventory_VaccineID')
    CREATE INDEX IX_VaccineInventory_VaccineID ON dbo.VaccineInventory (VaccineID);

/* ---------------------------------------------------------------------
   10. STATIONS
   --------------------------------------------------------------------- */
-- Station names could only be 10 characters long ("Station A" barely fit)
IF COL_LENGTH(N'dbo.ClinicRooms', N'RoomNumber') < 100
    ALTER TABLE dbo.ClinicRooms ALTER COLUMN RoomNumber nvarchar(50) NOT NULL;

-- The clinic calls its vaccination rooms "Room 1", "Room 2", ... Remove the
-- stations made during development (101, 102, 103, and Station A/B/C from an
-- older DemoSeed.sql) unless a patient is in one right now. Past visits that
-- used one keep their record, just without the room.
DECLARE @OldRooms TABLE (ID int PRIMARY KEY);
INSERT @OldRooms
SELECT r.RoomID FROM dbo.ClinicRooms r
WHERE r.RoomNumber IN (N'101', N'102', N'103', N'Station A', N'Station B', N'Station C')
  AND NOT EXISTS (SELECT 1 FROM dbo.Queues q
                  WHERE q.AssignedRoomID = r.RoomID AND q.Status = N'InProgress'
                    AND q.QueueDate = CAST(GETDATE() AS date));
UPDATE dbo.Queues SET AssignedRoomID = NULL WHERE AssignedRoomID IN (SELECT ID FROM @OldRooms);
DELETE FROM dbo.ClinicRooms WHERE RoomID IN (SELECT ID FROM @OldRooms);

-- Rooms 1–3 (the admin can add, rename or remove rooms in the app)
DECLARE @RoomNo int = 1;
WHILE @RoomNo <= 3
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.ClinicRooms WHERE RoomNumber = N'Room ' + CAST(@RoomNo AS nvarchar(3)))
        INSERT dbo.ClinicRooms (RoomNumber) VALUES (N'Room ' + CAST(@RoomNo AS nvarchar(3)));
    SET @RoomNo += 1;
END

-- A station is only busy while today's visit is still in progress there.
UPDATE r SET IsOccupied = 0, CurrentChildID = NULL
FROM dbo.ClinicRooms r
WHERE (r.IsOccupied = 1 OR r.CurrentChildID IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM dbo.Queues q
                  WHERE q.AssignedRoomID = r.RoomID
                    AND q.Status = N'InProgress'
                    AND q.QueueDate = CAST(GETDATE() AS date));

-- One station per health worker (keeps the busy one, else the first).
WITH w AS (
    SELECT RoomID, ROW_NUMBER() OVER (PARTITION BY AssignedDoctorID ORDER BY IsOccupied DESC, RoomID) AS rn
    FROM dbo.ClinicRooms WHERE AssignedDoctorID IS NOT NULL
)
UPDATE r SET AssignedDoctorID = NULL
FROM dbo.ClinicRooms r JOIN w ON w.RoomID = r.RoomID
WHERE w.rn > 1 AND r.IsOccupied = 0;

-- Vaccination days are Monday, Wednesday and Friday, 8 AM–12 PM (check-in
-- until 11 AM). Only replaces the old Mon–Fri 8 AM–5 PM default, so hours the
-- admin already set under Operating Hours are kept.
IF (SELECT COUNT(*) FROM dbo.ClinicOperatingSchedule
    WHERE IsActive = 1 AND IsOpen = 1 AND DayOfWeek BETWEEN 1 AND 5
      AND OpeningTime = '08:00' AND ClosingTime = '17:00') = 5
BEGIN
    UPDATE dbo.ClinicOperatingSchedule
    SET IsOpen = CASE WHEN DayOfWeek IN (1, 3, 5) THEN 1 ELSE 0 END,
        OpeningTime = '08:00', ClosingTime = '12:00', QueueCutoffTime = '11:00',
        UpdatedAt = SYSUTCDATETIME()
    WHERE DayOfWeek BETWEEN 1 AND 5;
    PRINT 'Vaccination days set to Mon, Wed, Fri 8:00 AM - 12:00 PM.';
END

COMMIT TRANSACTION;

PRINT 'Aruga database cleanup finished.';
