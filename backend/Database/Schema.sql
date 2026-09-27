/* =====================================================================
   ARUGA — FULL DATABASE SETUP (new installation)
   =====================================================================
   Creates ArugaSystemDB from nothing: every table the system uses, with
   its keys and rules, plus the starting data the system needs to work:

     • the 7 DOH EPI vaccines and their dose schedule rules
     • vaccination days: Monday, Wednesday, Friday, 8:00 AM – 12:00 PM
       (check-in until 11 AM; change these in the app: System Admin >
       Operating Hours)
     • 3 vaccination rooms (Room 1–3); staff pick who works in each today
     • QR check-in switched ON
     • one administrator account:
           username  admin
           password  Admin@2026      (the system asks for a new one at first login)

   Use this on a NEW computer / empty SQL Server. For an existing
   ArugaSystemDB, run Cleanup_2026-09.sql instead: it upgrades the old
   database to this same structure without touching your records.

   Optional afterwards: DemoSeed.sql (sample patients for demos).

   Run in SSMS (open the file, press Execute), or:
       sqlcmd -S <server> -E -i Schema.sql
   It is safe to run twice: anything that already exists is skipped.
   ===================================================================== */

IF DB_ID(N'ArugaSystemDB') IS NULL
    CREATE DATABASE ArugaSystemDB;
GO

USE ArugaSystemDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- needed for the filtered index below (sqlcmd defaults to OFF)
SET ANSI_NULLS ON;
GO

/* ---------------------------------------------------------------------
   PEOPLE AND LOGINS
   Users ........ health center personnel (Doctor, Nurse, Staff, Admin)
   Parents ...... parents / guardians
   Accounts ..... one login per person; ReferenceID points to Users or
                  Parents depending on AccountType
   AccountOTPs .. Forgot Password codes (hashed)
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserID        uniqueidentifier NOT NULL CONSTRAINT DF_Users_UserID DEFAULT (NEWID()),
    FirstName     nvarchar(50)     NOT NULL,
    MiddleName    nvarchar(50)     NULL,
    LastName      nvarchar(50)     NOT NULL,
    Username      nvarchar(50)     NOT NULL,
    PasswordHash  nvarchar(max)    NOT NULL,   -- not used for login (Accounts is); kept for older code
    ContactNo     nvarchar(20)     NULL,
    UserType      nvarchar(20)     NULL,
    PRCNo         nvarchar(50)     NULL,       -- professional license (Doctors / Nurses)
    AccountStatus nvarchar(20)     NULL CONSTRAINT DF_Users_AccountStatus DEFAULT ('Active'),
    Email         nvarchar(200)    NULL,
    Address       nvarchar(500)    NULL,
    CreatedAt     datetime         NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (GETDATE()),
    Position      nvarchar(20)     NULL,       -- Doctor / Nurse / Staff / Administrator: decides the portal
    CONSTRAINT PK_Users PRIMARY KEY (UserID),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT CK_Users_UserType CHECK (UserType IN (N'Doctor', N'Nurse', N'Admission', N'Administrator'))
);

IF OBJECT_ID(N'dbo.Parents', N'U') IS NULL
CREATE TABLE dbo.Parents (
    ParentID                   uniqueidentifier NOT NULL CONSTRAINT DF_Parents_ParentID DEFAULT (NEWID()),
    FirstName                  nvarchar(50)     NOT NULL,
    MiddleName                 nvarchar(50)     NULL,
    LastName                   nvarchar(50)     NOT NULL,
    Email                      nvarchar(255)    NULL,
    ContactNo                  nvarchar(20)     NOT NULL,
    BarangayNo                 nvarchar(20)     NULL,
    Address                    nvarchar(255)    NULL,
    CreatedAt                  datetime2        NOT NULL CONSTRAINT DF_Parents_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt                  datetime2        NOT NULL CONSTRAINT DF_Parents_UpdatedAt DEFAULT (GETDATE()),
    PasswordHash               nvarchar(255)    NULL,   -- not used for login (Accounts is); kept for older code
    MustChangePassword         bit              NOT NULL CONSTRAINT DF_Parents_MustChangePassword DEFAULT (0),
    TemporaryPasswordExpiresAt datetime2        NULL,
    LastLogin                  datetime2        NULL,
    CONSTRAINT PK_Parents PRIMARY KEY (ParentID),
    CONSTRAINT UQ_Parents_Email UNIQUE (Email)
);

IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    AccountID           uniqueidentifier NOT NULL CONSTRAINT DF_Accounts_AccountID DEFAULT (NEWID()),
    Username            nvarchar(100)    NOT NULL,   -- a parent's email, or a staff username
    PasswordHash        nvarchar(max)    NOT NULL,   -- BCrypt
    AccountType         nvarchar(20)     NOT NULL,   -- Personnel / Parent
    ReferenceID         uniqueidentifier NOT NULL,   -- Users.UserID or Parents.ParentID
    Status              bit              NOT NULL CONSTRAINT DF_Accounts_Status DEFAULT (1),
    MustChangePassword  bit              NOT NULL CONSTRAINT DF_Accounts_MustChangePassword DEFAULT (1),
    FailedLoginAttempts int              NOT NULL CONSTRAINT DF_Accounts_FailedLoginAttempts DEFAULT (0),
    LockedUntil         datetime2        NULL,
    LastLogin           datetime2        NULL,
    CreatedAt           datetime2        NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt           datetime2        NULL,
    CONSTRAINT PK_Accounts PRIMARY KEY (AccountID),
    CONSTRAINT UQ_Accounts_Username UNIQUE (Username),
    CONSTRAINT CK_Accounts_AccountType CHECK (AccountType IN ('Personnel', 'Parent'))
);

IF OBJECT_ID(N'dbo.AccountOTPs', N'U') IS NULL
CREATE TABLE dbo.AccountOTPs (
    OTPID     uniqueidentifier NOT NULL CONSTRAINT DF_AccountOTPs_OTPID DEFAULT (NEWID()),
    AccountID uniqueidentifier NOT NULL,
    OTPHash   nvarchar(255)    NOT NULL,
    Purpose   nvarchar(50)     NOT NULL,   -- PasswordReset
    ExpiresAt datetime2        NOT NULL,
    IsUsed    bit              NOT NULL CONSTRAINT DF_AccountOTPs_IsUsed DEFAULT (0),
    Attempts  int              NOT NULL CONSTRAINT DF_AccountOTPs_Attempts DEFAULT (0),
    CreatedAt datetime2        NOT NULL CONSTRAINT DF_AccountOTPs_CreatedAt DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_AccountOTPs PRIMARY KEY (OTPID),
    CONSTRAINT FK_AccountOTPs_Accounts FOREIGN KEY (AccountID) REFERENCES dbo.Accounts (AccountID) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccountOTPs_AccountID')
    CREATE INDEX IX_AccountOTPs_AccountID ON dbo.AccountOTPs (AccountID, Purpose);

/* ---------------------------------------------------------------------
   CHILDREN (patients) AND THEIR PARENTS
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Children', N'U') IS NULL
CREATE TABLE dbo.Children (
    ChildID      uniqueidentifier NOT NULL CONSTRAINT DF_Children_ChildID DEFAULT (NEWID()),
    FirstName    nvarchar(50)     NOT NULL,
    MiddleName   nvarchar(50)     NULL,
    LastName     nvarchar(50)     NOT NULL,
    BirthDate    date             NOT NULL,
    PlaceOfBirth nvarchar(100)    NULL,
    Address      nvarchar(70)     NULL,
    HealthCenter nvarchar(100)    NULL,
    Barangay     int              NULL,
    Sex          nvarchar(10)     NULL,
    CreatedAt    datetime2        NOT NULL CONSTRAINT DF_Children_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt    datetime2        NOT NULL CONSTRAINT DF_Children_UpdatedAt DEFAULT (GETDATE()),
    Allergies    nvarchar(500)    NULL,
    ExistingConditions nvarchar(500) NULL, -- e.g. asthma; Doctors/Nurses may update it with Allergies
    BirthHeight  decimal(5,2)     NULL,   -- cm
    BirthWeight  decimal(5,2)     NULL,   -- kg
    FamilyNo     nvarchar(20)     NULL,   -- family (household) number the clinic files by
    CONSTRAINT PK_Children PRIMARY KEY (ChildID)
);

IF OBJECT_ID(N'dbo.ChildParentRelationship', N'U') IS NULL
CREATE TABLE dbo.ChildParentRelationship (
    RelationshipID          uniqueidentifier NOT NULL CONSTRAINT DF_ChildParentRelationship_RelationshipID DEFAULT (NEWID()),
    ChildID                 uniqueidentifier NOT NULL,
    ParentID                uniqueidentifier NOT NULL,
    RelationshipType        nvarchar(50)     NOT NULL,   -- Mother / Father / Guardian
    IsPrimaryContact        bit              NOT NULL CONSTRAINT DF_ChildParentRelationship_IsPrimaryContact DEFAULT (0),
    CanReceiveNotifications bit              NOT NULL CONSTRAINT DF_ChildParentRelationship_CanReceiveNotifications DEFAULT (1),
    Status                  nvarchar(20)     NOT NULL CONSTRAINT DF_ChildParentRelationship_Status DEFAULT ('Active'),
    CreatedAt               datetime2        NOT NULL CONSTRAINT DF_ChildParentRelationship_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt               datetime2        NULL,
    CONSTRAINT PK_ChildParentRelationship PRIMARY KEY (RelationshipID),
    CONSTRAINT FK_Relationship_Child FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT FK_Relationship_Parent FOREIGN KEY (ParentID) REFERENCES dbo.Parents (ParentID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChildParentRelationship_ParentID')
    CREATE INDEX IX_ChildParentRelationship_ParentID ON dbo.ChildParentRelationship (ParentID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChildParentRelationship_ChildID')
    CREATE INDEX IX_ChildParentRelationship_ChildID ON dbo.ChildParentRelationship (ChildID);

/* ---------------------------------------------------------------------
   VACCINES, SCHEDULE RULES AND STOCK
   VaccinationScheduleRules drives each child's timeline (age in days,
   minimum interval from the previous dose). VaccineDoses holds the dose
   list the admin Vaccines page edits alongside the rules.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Vaccines', N'U') IS NULL
CREATE TABLE dbo.Vaccines (
    VaccineID             int            IDENTITY(1,1) NOT NULL,
    VaccineName           nvarchar(100)  NOT NULL,
    Description           nvarchar(max)  NULL,
    Abbreviation          nvarchar(20)   NULL,
    TargetDisease         nvarchar(200)  NULL,
    RecommendedAge        nvarchar(100)  NULL,
    AgeCategory           nvarchar(50)   NULL,
    NumberOfRequiredDoses int            NOT NULL CONSTRAINT DF_Vaccines_NumberOfRequiredDoses DEFAULT (1),
    DoseInterval          nvarchar(100)  NULL,
    AdministrationRoute   nvarchar(100)  NULL,
    Status                bit            NOT NULL CONSTRAINT DF_Vaccines_Status DEFAULT (1),
    CreatedAt             datetime2      NOT NULL CONSTRAINT DF_Vaccines_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt             datetime2      NULL,
    CONSTRAINT PK_Vaccines PRIMARY KEY (VaccineID)
);

IF OBJECT_ID(N'dbo.VaccineDoses', N'U') IS NULL
CREATE TABLE dbo.VaccineDoses (
    DoseID          int IDENTITY(1,1) NOT NULL,
    VaccineID       int NULL,
    DoseNumber      int NOT NULL,
    MinIntervalDays int NULL,
    CONSTRAINT PK_VaccineDoses PRIMARY KEY (DoseID),
    CONSTRAINT FK_VaccineDoses_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID)
);

IF OBJECT_ID(N'dbo.VaccinationScheduleRules', N'U') IS NULL
CREATE TABLE dbo.VaccinationScheduleRules (
    RuleID                       int       IDENTITY(1,1) NOT NULL,
    VaccineID                    int       NOT NULL,
    DoseNumber                   int       NOT NULL,
    RecommendedAgeDays           int       NOT NULL,
    MinimumAgeDays               int       NOT NULL,
    IntervalFromPreviousDoseDays int       NOT NULL,
    SequenceOrder                int       NOT NULL,
    IsRequired                   bit       NOT NULL CONSTRAINT DF_VaccinationScheduleRules_IsRequired DEFAULT (1),
    CreatedAt                    datetime2 NOT NULL CONSTRAINT DF_VaccinationScheduleRules_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt                    datetime2 NULL,
    CONSTRAINT PK_VaccinationScheduleRules PRIMARY KEY (RuleID),
    CONSTRAINT UQ_VaccineDose UNIQUE (VaccineID, DoseNumber),
    CONSTRAINT FK_VaccinationScheduleRules_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID)
);

IF OBJECT_ID(N'dbo.VaccineInventory', N'U') IS NULL
CREATE TABLE dbo.VaccineInventory (
    InventoryID       int           IDENTITY(1,1) NOT NULL,
    VaccineID         int           NOT NULL,
    LotNumber         nvarchar(100) NOT NULL,
    InitialQuantity   int           NOT NULL,
    CurrentQuantity   int           NOT NULL,
    MinimumStock      int           NOT NULL CONSTRAINT DF_VaccineInventory_MinimumStock DEFAULT (20),
    ExpirationDate    date          NOT NULL,
    ReceivedDate      date          NOT NULL,
    Supplier          nvarchar(255) NULL,
    Status            bit           NOT NULL CONSTRAINT DF_VaccineInventory_Status DEFAULT (1),   -- active batch
    CreatedAt         datetime      NOT NULL CONSTRAINT DF_VaccineInventory_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt         datetime      NULL,
    ManufacturingDate date          NULL,
    CONSTRAINT PK_VaccineInventory PRIMARY KEY (InventoryID),
    CONSTRAINT FK_VaccineInventory_Vaccine FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccineInventory_VaccineID')
    CREATE INDEX IX_VaccineInventory_VaccineID ON dbo.VaccineInventory (VaccineID);

/* ---------------------------------------------------------------------
   EACH CHILD'S SCHEDULE AND GIVEN DOSES
   VaccinationTimeline: one row per dose the child should get, with the
   date it is due (recalculated after each vaccination: 28-day rule).
   VaccinationRecords: doses actually given. NurseObservation is shown in
   the app as "Remarks" (reactions / complications after the vaccine).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.VaccinationRecords', N'U') IS NULL
CREATE TABLE dbo.VaccinationRecords (
    VaccinationRecordID  uniqueidentifier NOT NULL CONSTRAINT DF_VaccinationRecords_VaccinationRecordID DEFAULT (NEWID()),
    RecordCode           nvarchar(25)     NOT NULL,
    ChildID              uniqueidentifier NOT NULL,
    VaccineID            int              NOT NULL,
    InventoryID          int              NULL,    -- which batch (lot number) the dose came from
    TimelineID           uniqueidentifier NULL,
    DoseNumber           int              NOT NULL,
    VaccinationDate      datetime2        NOT NULL,
    AdministeredByUserID uniqueidentifier NULL,
    NurseObservation     nvarchar(max)    NULL,    -- "Remarks"
    Status               nvarchar(20)     NOT NULL CONSTRAINT DF_VaccinationRecords_Status DEFAULT ('Completed'),
    CreatedAt            datetime2        NOT NULL CONSTRAINT DF_VaccinationRecords_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt            datetime2        NULL,
    CONSTRAINT PK_VaccinationRecords PRIMARY KEY (VaccinationRecordID),
    CONSTRAINT UQ_VaccinationRecords_RecordCode UNIQUE (RecordCode),
    CONSTRAINT UQ_VaccinationRecord_ChildDose UNIQUE (ChildID, VaccineID, DoseNumber),
    CONSTRAINT FK_VaccinationRecords_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT FK_VaccinationRecords_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID),
    CONSTRAINT FK_VaccinationRecords_Inventory FOREIGN KEY (InventoryID) REFERENCES dbo.VaccineInventory (InventoryID),
    CONSTRAINT FK_VaccinationRecords_AdministeredBy FOREIGN KEY (AdministeredByUserID) REFERENCES dbo.Users (UserID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccinationRecords_VaccinationDate')
    CREATE INDEX IX_VaccinationRecords_VaccinationDate ON dbo.VaccinationRecords (VaccinationDate);

IF OBJECT_ID(N'dbo.VaccinationTimeline', N'U') IS NULL
CREATE TABLE dbo.VaccinationTimeline (
    TimelineID          uniqueidentifier NOT NULL CONSTRAINT DF_VaccinationTimeline_TimelineID DEFAULT (NEWID()),
    TimelineCode        nvarchar(25)     NOT NULL,
    ChildID             uniqueidentifier NOT NULL,
    VaccineID           int              NOT NULL,
    DoseNumber          int              NOT NULL,
    ExpectedDate        date             NOT NULL,   -- birth date + recommended age
    ScheduledDate       date             NOT NULL,   -- moved to an open clinic day / after the previous dose
    CompletedDate       date             NULL,
    VaccinationRecordID uniqueidentifier NULL,
    Status              nvarchar(20)     NOT NULL CONSTRAINT DF_VaccinationTimeline_Status DEFAULT ('Pending'),
    CreatedAt           datetime2        NOT NULL CONSTRAINT DF_VaccinationTimeline_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt           datetime2        NULL,
    CONSTRAINT PK_VaccineTimeline PRIMARY KEY (TimelineID),
    CONSTRAINT UQ_VaccineTimeline_Code UNIQUE (TimelineCode),
    CONSTRAINT UQ_VaccineTimeline_ChildDose UNIQUE (ChildID, VaccineID, DoseNumber),
    CONSTRAINT FK_VaccineTimeline_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT FK_VaccineTimeline_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID),
    CONSTRAINT FK_VaccineTimeline_Record FOREIGN KEY (VaccinationRecordID) REFERENCES dbo.VaccinationRecords (VaccinationRecordID),
    CONSTRAINT CK_VaccineTimeline_Status CHECK (Status IN ('Pending', 'Due', 'Completed', 'Overdue', 'Missed', 'Cancelled'))
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VaccinationTimeline_ScheduledDate')
    CREATE INDEX IX_VaccinationTimeline_ScheduledDate ON dbo.VaccinationTimeline (ScheduledDate, Status);

/* ---------------------------------------------------------------------
   CLINIC DAYS, STATIONS AND THE QUEUE
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ClinicOperatingSchedule', N'U') IS NULL
CREATE TABLE dbo.ClinicOperatingSchedule (
    ScheduleID      int       IDENTITY(1,1) NOT NULL,
    DayOfWeek       int       NOT NULL,   -- 0 = Sunday ... 6 = Saturday
    IsOpen          bit       NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_IsOpen DEFAULT (0),
    OpeningTime     time      NOT NULL,
    ClosingTime     time      NOT NULL,
    QueueCutoffTime time      NOT NULL,   -- last time a patient can check in
    IsActive        bit       NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_IsActive DEFAULT (1),
    CreatedAt       datetime2 NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt       datetime2 NULL,
    CONSTRAINT PK_ClinicOperatingSchedule PRIMARY KEY (ScheduleID),
    CONSTRAINT CK_ClinicOperatingSchedule_DayOfWeek CHECK (DayOfWeek BETWEEN 0 AND 6),
    CONSTRAINT CK_ClinicOperatingSchedule_Time CHECK (OpeningTime < ClosingTime AND QueueCutoffTime >= OpeningTime AND QueueCutoffTime <= ClosingTime)
);

-- Holidays and special openings; override the weekly schedule on that date
IF OBJECT_ID(N'dbo.ClinicScheduleExceptions', N'U') IS NULL
CREATE TABLE dbo.ClinicScheduleExceptions (
    ExceptionID     int           IDENTITY(1,1) NOT NULL,
    ExceptionDate   date          NOT NULL,
    IsOpen          bit           NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_IsOpen DEFAULT (0),
    OpeningTime     time          NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_OpeningTime DEFAULT ('07:00:00'),
    ClosingTime     time          NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_ClosingTime DEFAULT ('12:00:00'),
    QueueCutoffTime time          NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_QueueCutoffTime DEFAULT ('10:00:00'),
    Reason          nvarchar(255) NULL,
    IsActive        bit           NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_IsActive DEFAULT (1),
    CreatedAt       datetime2     NOT NULL CONSTRAINT DF_ClinicScheduleExceptions_CreatedAt DEFAULT (GETUTCDATE()),
    UpdatedAt       datetime2     NULL,
    CONSTRAINT PK_ClinicScheduleExceptions PRIMARY KEY (ExceptionID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClinicScheduleExceptions_ExceptionDate')
    CREATE UNIQUE INDEX IX_ClinicScheduleExceptions_ExceptionDate ON dbo.ClinicScheduleExceptions (ExceptionDate) WHERE IsActive = 1;

-- Vaccination stations. AssignedDoctorID = the Doctor or Nurse at the
-- station today (chosen by the Admission Staff on their dashboard).
IF OBJECT_ID(N'dbo.ClinicRooms', N'U') IS NULL
CREATE TABLE dbo.ClinicRooms (
    RoomID           int              IDENTITY(1,1) NOT NULL,
    RoomNumber       nvarchar(50)     NOT NULL,   -- station name
    AssignedDoctorID uniqueidentifier NULL,
    IsOccupied       bit              NOT NULL CONSTRAINT DF_ClinicRooms_IsOccupied DEFAULT (0),
    CurrentChildID   uniqueidentifier NULL,
    CONSTRAINT PK_ClinicRooms PRIMARY KEY (RoomID),
    CONSTRAINT FK_ClinicRooms_AssignedDoctorID FOREIGN KEY (AssignedDoctorID) REFERENCES dbo.Users (UserID),
    CONSTRAINT FK_ClinicRooms_CurrentChildID FOREIGN KEY (CurrentChildID) REFERENCES dbo.Children (ChildID)
);

-- One row per family visit per day; QueueChildren lists the children in it
IF OBJECT_ID(N'dbo.Queues', N'U') IS NULL
CREATE TABLE dbo.Queues (
    QueueID        uniqueidentifier NOT NULL,
    ParentID       uniqueidentifier NOT NULL,
    QueueNumber    int              NOT NULL,
    QueueDate      date             NOT NULL,
    Status         nvarchar(20)     NOT NULL CONSTRAINT DF_Queues_Status DEFAULT ('Waiting'),   -- Waiting / InProgress / Completed
    AssignedRoomID int              NULL,
    CreatedAt      datetime         NOT NULL CONSTRAINT DF_Queues_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt      datetime         NULL,
    CONSTRAINT PK_Queues PRIMARY KEY (QueueID),
    CONSTRAINT FK_Queues_Parents FOREIGN KEY (ParentID) REFERENCES dbo.Parents (ParentID),
    CONSTRAINT FK_Queues_ClinicRooms FOREIGN KEY (AssignedRoomID) REFERENCES dbo.ClinicRooms (RoomID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Queues_QueueDate')
    CREATE INDEX IX_Queues_QueueDate ON dbo.Queues (QueueDate, Status);

IF OBJECT_ID(N'dbo.QueueChildren', N'U') IS NULL
CREATE TABLE dbo.QueueChildren (
    QueueChildID uniqueidentifier NOT NULL,
    QueueID      uniqueidentifier NOT NULL,
    ChildID      uniqueidentifier NOT NULL,
    CreatedAt    datetime         NOT NULL CONSTRAINT DF_QueueChildren_CreatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_QueueChildren PRIMARY KEY (QueueChildID),
    CONSTRAINT FK_QueueChildren_Queues FOREIGN KEY (QueueID) REFERENCES dbo.Queues (QueueID),
    CONSTRAINT FK_QueueChildren_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueChildren_QueueID')
    CREATE INDEX IX_QueueChildren_QueueID ON dbo.QueueChildren (QueueID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueChildren_ChildID')
    CREATE INDEX IX_QueueChildren_ChildID ON dbo.QueueChildren (ChildID);

-- QR check-in: IsEnabled = parents must scan today's code to check in
IF OBJECT_ID(N'dbo.QueueQRSettings', N'U') IS NULL
CREATE TABLE dbo.QueueQRSettings (
    SettingID int       IDENTITY(1,1) NOT NULL,
    IsEnabled bit       NOT NULL CONSTRAINT DF_QueueQRSettings_IsEnabled DEFAULT (1),
    UpdatedAt datetime2 NOT NULL CONSTRAINT DF_QueueQRSettings_UpdatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_QueueQRSettings PRIMARY KEY (SettingID)
);

-- One code per clinic day, valid from opening time to the check-in cut-off
IF OBJECT_ID(N'dbo.QueueQRCodes', N'U') IS NULL
CREATE TABLE dbo.QueueQRCodes (
    QRCodeID   uniqueidentifier NOT NULL CONSTRAINT DF_QueueQRCodes_QRCodeID DEFAULT (NEWID()),
    Token      nvarchar(64)     NOT NULL,   -- inside the QR
    ShortCode  nvarchar(10)     NOT NULL,   -- printed under the QR, for typing in
    QRDate     date             NOT NULL,
    ValidFrom  datetime2        NOT NULL,
    ValidUntil datetime2        NOT NULL,
    IsActive   bit              NOT NULL CONSTRAINT DF_QueueQRCodes_IsActive DEFAULT (1),
    CreatedAt  datetime2        NOT NULL CONSTRAINT DF_QueueQRCodes_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_QueueQRCodes PRIMARY KEY (QRCodeID),
    CONSTRAINT UQ_QueueQRCodes_Token UNIQUE (Token)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QueueQRCodes_QRDate')
    CREATE INDEX IX_QueueQRCodes_QRDate ON dbo.QueueQRCodes (QRDate);

/* ---------------------------------------------------------------------
   NOTIFICATIONS AND AUDIT TRAIL
   Notifications: parent-facing rows use ParentID, staff-facing rows
   (e.g. low stock) use UserID.
   AuditLogs: one JSON entry per important action (who, what, old/new).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
CREATE TABLE dbo.Notifications (
    NotificationID uniqueidentifier NOT NULL CONSTRAINT DF_Notifications_NotificationID DEFAULT (NEWID()),
    ParentID       uniqueidentifier NULL,
    ChildID        uniqueidentifier NULL,
    VaccineID      int              NULL,
    DoseNumber     int              NULL,
    Type           nvarchar(50)     NOT NULL,
    Title          nvarchar(200)    NOT NULL,
    Message        nvarchar(500)    NOT NULL,
    ScheduledDate  date             NULL,
    IsRead         bit              NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt      datetime         NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (GETDATE()),
    UserID         uniqueidentifier NULL,
    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_ParentID')
    CREATE INDEX IX_Notifications_ParentID ON dbo.Notifications (ParentID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_UserID')
    CREATE INDEX IX_Notifications_UserID ON dbo.Notifications (UserID);

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
CREATE TABLE dbo.AuditLogs (
    AuditID         bigint           IDENTITY(1,1) NOT NULL,
    UserID          uniqueidentifier NULL,
    ActionPerformed nvarchar(max)    NOT NULL,
    ActionDate      datetime         NULL CONSTRAINT DF_AuditLogs_ActionDate DEFAULT (GETDATE()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY (AuditID),
    CONSTRAINT FK_AuditLogs_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users (UserID)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_ActionDate')
    CREATE INDEX IX_AuditLogs_ActionDate ON dbo.AuditLogs (ActionDate);
GO

/* =====================================================================
   STARTING DATA (only added when the table is empty)
   ===================================================================== */

-- DOH Expanded Program on Immunization vaccines (same IDs as the
-- original database, so both setups match exactly)
IF NOT EXISTS (SELECT 1 FROM dbo.Vaccines)
BEGIN
    SET IDENTITY_INSERT dbo.Vaccines ON;
    INSERT dbo.Vaccines (VaccineID, VaccineName, Abbreviation, TargetDisease, RecommendedAge, AgeCategory, NumberOfRequiredDoses, DoseInterval, AdministrationRoute, Status, Description) VALUES
     (5,  N'BCG',                             N'BCG',   N'Tuberculosis',                             N'At birth',           N'Infant', 1, NULL,                       N'Intradermal',                  1, N'Bacillus Calmette-Guerin vaccine'),
     (6,  N'Hepatitis B',                     N'HepB',  N'Hepatitis B',                              N'At birth',           N'Infant', 1, NULL,                       N'Intramuscular',                1, N'Hepatitis B vaccine'),
     (7,  N'Pentavalent (DPT-HepB-Hib)',      N'Penta', N'Diphtheria, Pertussis, Tetanus, HepB, Hib', N'6-14 weeks',        N'Infant', 3, N'4 weeks between doses',   N'Intramuscular',                1, N'Combination vaccine'),
     (8,  N'Oral Polio Vaccine',              N'OPV',   N'Poliomyelitis',                            N'6-14 weeks',         N'Infant', 3, N'4 weeks between doses',   N'Oral',                         1, N'Oral polio vaccine'),
     (9,  N'Inactivated Polio Vaccine',       N'IPV',   N'Poliomyelitis',                            N'3½–9 months',        N'Infant', 2, N'~5 months between doses', N'Intramuscular (IM) injection', 1, N'IPV vaccine'),
     (10, N'Pneumococcal Conjugate Vaccine',  N'PCV',   N'Pneumonia, Meningitis',                    N'6-14 weeks',         N'Infant', 3, N'4 weeks between doses',   N'Intramuscular (IM) injection', 1, N'PCV vaccine'),
     (11, N'Measles, Mumps, Rubella Vaccine', N'MMR',   N'Measles, Mumps, Rubella',                  N'9 months and older', N'Child',  2, N'3 months between doses',  N'Subcutaneous (SC) injection',  1, N'MMR vaccine');
    SET IDENTITY_INSERT dbo.Vaccines OFF;
END

IF NOT EXISTS (SELECT 1 FROM dbo.VaccineDoses)
BEGIN
    SET IDENTITY_INSERT dbo.VaccineDoses ON;
    INSERT dbo.VaccineDoses (DoseID, VaccineID, DoseNumber, MinIntervalDays) VALUES
     (10, 5, 1, NULL), (11, 6, 1, NULL),
     (12, 7, 1, NULL), (13, 7, 2, 28), (14, 7, 3, 28),
     (15, 8, 1, NULL), (16, 8, 2, 28), (17, 8, 3, 28),
     (18, 9, 1, NULL), (19, 9, 2, 168),
     (20, 10, 1, NULL), (21, 10, 2, 28), (22, 10, 3, 28),
     (23, 11, 1, NULL), (24, 11, 2, 90);
    SET IDENTITY_INSERT dbo.VaccineDoses OFF;
END

-- Age (days after birth) each dose is due, and the minimum gap after the
-- previous dose. The system never schedules two doses closer than 28 days.
IF NOT EXISTS (SELECT 1 FROM dbo.VaccinationScheduleRules)
BEGIN
    SET IDENTITY_INSERT dbo.VaccinationScheduleRules ON;
    INSERT dbo.VaccinationScheduleRules (RuleID, VaccineID, DoseNumber, RecommendedAgeDays, MinimumAgeDays, IntervalFromPreviousDoseDays, SequenceOrder, IsRequired) VALUES
     (1,  5, 1,   0,   0,   0,  1, 1),   -- BCG            at birth
     (2,  6, 1,   0,   0,   0,  2, 1),   -- Hepatitis B    at birth
     (3,  7, 1,  42,  42,   0,  3, 1),   -- Penta 1        6 weeks
     (4,  8, 1,  42,  42,   0,  4, 1),   -- OPV 1          6 weeks
     (5, 10, 1,  42,  42,   0,  5, 1),   -- PCV 1          6 weeks
     (6,  7, 2,  70,  70,  28,  6, 1),   -- Penta 2        10 weeks
     (7,  8, 2,  70,  70,  28,  7, 1),   -- OPV 2          10 weeks
     (8, 10, 2,  70,  70,  28,  8, 1),   -- PCV 2          10 weeks
     (9,  7, 3,  98,  98,  28,  9, 1),   -- Penta 3        14 weeks
     (10, 8, 3,  98,  98,  28, 10, 1),   -- OPV 3          14 weeks
     (11,10, 3,  98,  98,  28, 11, 1),   -- PCV 3          14 weeks
     (12, 9, 1,  98,  98,   0, 12, 1),   -- IPV 1          14 weeks
     (13,11, 1, 270, 270,   0, 13, 1),   -- MMR 1          9 months
     (14, 9, 2, 270, 270, 172, 14, 1),   -- IPV 2          9 months
     (15,11, 2, 365, 365,  95, 15, 1);   -- MMR 2          12 months
    SET IDENTITY_INSERT dbo.VaccinationScheduleRules OFF;
END

-- Vaccination days: Monday, Wednesday and Friday, 8 AM–12 PM, check-in until 11 AM
IF NOT EXISTS (SELECT 1 FROM dbo.ClinicOperatingSchedule)
    INSERT dbo.ClinicOperatingSchedule (DayOfWeek, IsOpen, OpeningTime, ClosingTime, QueueCutoffTime) VALUES
     (1, 1, '08:00', '12:00', '11:00'),
     (2, 0, '08:00', '12:00', '11:00'),
     (3, 1, '08:00', '12:00', '11:00'),
     (4, 0, '08:00', '12:00', '11:00'),
     (5, 1, '08:00', '12:00', '11:00'),
     (6, 0, '08:00', '12:00', '11:00'),
     (0, 0, '08:00', '12:00', '11:00');

IF NOT EXISTS (SELECT 1 FROM dbo.QueueQRSettings)
    INSERT dbo.QueueQRSettings (IsEnabled) VALUES (1);

IF NOT EXISTS (SELECT 1 FROM dbo.ClinicRooms)
    INSERT dbo.ClinicRooms (RoomNumber) VALUES (N'Room 1'), (N'Room 2'), (N'Room 3');

-- First administrator. Password: Admin@2026 (must be changed at first login)
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Position = N'Administrator')
BEGIN
    DECLARE @AdminID uniqueidentifier = NEWID();
    DECLARE @AdminPwd nvarchar(100) = N'$2a$11$.SPOiqhQhzpsR9YqVizdeOWDIQXpDIxWElCtDvBfnBGNKcvqXAZ.G';

    INSERT dbo.Users (UserID, FirstName, LastName, Username, PasswordHash, UserType, Position, AccountStatus, Email)
    VALUES (@AdminID, N'System', N'Administrator', N'admin', @AdminPwd, N'Administrator', N'Administrator', N'Active', NULL);

    IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE Username = N'admin')
        INSERT dbo.Accounts (Username, PasswordHash, AccountType, ReferenceID, Status, MustChangePassword)
        VALUES (N'admin', @AdminPwd, 'Personnel', @AdminID, 1, 1);
END
GO

-- If this SQL Server has the team's "ArugaSystem" login (appsettings.json with
-- User Id=ArugaSystem instead of Trusted_Connection=True), let it use the database.
IF SUSER_ID(N'ArugaSystem') IS NOT NULL AND USER_ID(N'ArugaSystem') IS NULL
BEGIN
    CREATE USER [ArugaSystem] FOR LOGIN [ArugaSystem] WITH DEFAULT_SCHEMA = dbo;
    ALTER ROLE db_owner ADD MEMBER [ArugaSystem];
END
GO

PRINT 'ArugaSystemDB is ready. Sign in as  admin / Admin@2026  and change the password.';
