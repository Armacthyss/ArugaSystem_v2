/* =====================================================================
   ARUGA — ONE-STEP SETUP WITH DEMO DATA (new computer)
   =====================================================================
   Open this file in SQL Server Management Studio and press Execute.
   It does both steps at once:
     1. Schema.sql    creates ArugaSystemDB (tables, the 7 EPI vaccines,
                      vaccination hours, and the admin account
                      admin / Admin@2026)
     2. DemoSeed.sql  adds the demo families, children, vaccination
                      history, vaccine stock and today's queue

   Super Admins (password SuperAdmin@2026, changed at first sign-in):
     gabriel_barlam, renzo_palmon

   Demo logins (password Aruga@2026):
     Admin / Doctor .. demo.doctor
     Staff / Nurse ... demo.nurse, demo.nurse2
     Parents ......... e.g. maria.santos@demo.aruga.ph

   Command line:  sqlcmd -S localhost -E -I -i Setup_WithDemoData.sql

   This file is Schema.sql + DemoSeed.sql joined together. If either of
   those changes, rebuild this file from them (see README.md).
   Demo dates are counted from the day you run it; to refresh them
   later, run DemoSeed.sql again on its own.
   ===================================================================== */

/* ============================ PART 1: Schema.sql ============================ */

/* =====================================================================
   ARUGA — FULL DATABASE SETUP (new installation)
   =====================================================================
   Creates ArugaSystemDB from nothing: every table the system uses, with
   its keys and rules, plus the starting data the system needs to work:

     • the 7 DOH EPI vaccines and their dose schedule rules
     • vaccination days: Monday, Wednesday, Friday, 8:00 AM – 12:00 PM
       (check-in until 11 AM; change these in the app: System Admin >
       Operating Hours)
     • QR check-in switched ON
     • two Super Admin accounts (the development team, see SuperAdmins.sql):
           gabriel_barlam, renzo_palmon   password  SuperAdmin@2026
     • one Admin / Doctor account:
           username  admin
           password  Admin@2026      (the system asks for a new one at first login;
                                      put the Doctor's real name on it in User Management)

   Use this on a NEW computer / empty SQL Server. For an existing
   ArugaSystemDB, run Cleanup_2026-09.sql instead: it upgrades the old
   database to this same structure without touching your records.

   Optional afterwards: DemoSeed.sql (sample patients for demos).

   Run in SSMS (open the file, press Execute), or:
       sqlcmd -S <server> -E -I -i Schema.sql
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
    ConsentRecordedAt          datetime2        NULL,   -- staff recorded the Data Privacy consent at registration
    PrivacyConsentAt           datetime2        NULL,   -- parent accepted the Data Privacy Notice in the app
    CONSTRAINT PK_Parents PRIMARY KEY (ParentID)
);
-- Email is optional (guardians without a portal login); real emails are unique
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Parents_Email')
    CREATE UNIQUE INDEX UX_Parents_Email ON dbo.Parents (Email) WHERE Email IS NOT NULL;

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
    InjectionSite        nvarchar(30)     NULL,    -- Left thigh / Right upper arm / Mouth (oral)...
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

-- First Admin / Doctor account (the Doctor is the admin level). Username admin,
-- password Admin@2026 (must be changed at first login). Put the Doctor's real
-- name and PRC license on it in User Management.
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Position IN (N'Doctor', N'Administrator'))
BEGIN
    DECLARE @AdminID uniqueidentifier = NEWID();
    DECLARE @AdminPwd nvarchar(100) = N'$2a$11$.SPOiqhQhzpsR9YqVizdeOWDIQXpDIxWElCtDvBfnBGNKcvqXAZ.G';

    INSERT dbo.Users (UserID, FirstName, LastName, Username, PasswordHash, UserType, Position, AccountStatus, Email)
    VALUES (@AdminID, N'Clinic', N'Doctor', N'admin', @AdminPwd, N'Doctor', N'Doctor', N'Active', NULL);

    IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE Username = N'admin')
        INSERT dbo.Accounts (Username, PasswordHash, AccountType, ReferenceID, Status, MustChangePassword)
        VALUES (N'admin', @AdminPwd, 'Personnel', @AdminID, 1, 1);
END
GO

-- Super Admin accounts: the development team (same as SuperAdmins.sql).
-- They see the audit logs and manage the Admin / Doctor accounts, not patients.
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
GO

/* ============================ PART 2: DemoSeed.sql ========================== */

/* =====================================================================
   ARUGA — DEMO DATA SEED
   =====================================================================
   Fills ArugaSystemDB with realistic sample data for demos and for the
   beneficiary test run. Everything is REAL data that goes through the
   normal tables, so every screen keeps working exactly as it does with
   live data (recording a vaccination, queueing, reports, ...).

   • Safe to re-run. Running it again refreshes the demo data: ages,
     schedules and today's queue are all computed from the day you run it,
     so run it on the morning of the demo to get a live "today".
   • Only touches its own rows. Every demo person uses an ID starting with
     A2A1…A2A7 and every demo vaccine batch a lot number starting DEMO-.
     Your existing records are never modified or deleted.
   • Demo families live in Barangays 19 and 21–40, the only barangays
     Leveriza Health Center serves.
   • To remove everything again, run DemoSeed_Remove.sql.

   DEMO LOGINS  (password for ALL of them:  Aruga@2026)
     Admin / Doctor ....... demo.doctor
     Staff / Nurse ........ demo.nurse  /  demo.nurse2
     Parents .............. e.g. maria.santos@demo.aruga.ph
                            (any parent email listed below)
     Grandmother .......... lourdes.luna@demo.aruga.ph (Isabela Cruz's
                            second guardian, to show a relative checking in)

   Open in SSMS and press Execute: it switches to ArugaSystemDB by itself
   (or: sqlcmd -S <server> -E -I -i DemoSeed.sql)
   ===================================================================== */

USE ArugaSystemDB;   -- if the database doesn't exist, nothing below runs

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Manila time, whatever time zone the database server uses (the live one runs on UTC)
DECLARE @Now datetime2 = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+08:00') AS datetime2);
DECLARE @Today date = CAST(@Now AS date);

-- BCrypt hash of  Aruga@2026
DECLARE @Pwd nvarchar(100) = N'$2a$11$eHcMC19WEWltSXIhNaYxz.TP9lkgL2aKbbRPTdhLpUjZgwrT2SE0C';

-- ---------------------------------------------------------------------
-- Reference data this script depends on
-- ---------------------------------------------------------------------
DECLARE @BCG int   = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'BCG'   OR VaccineName LIKE 'BCG%');
DECLARE @HepB int  = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'HepB'  OR VaccineName LIKE 'Hepatitis B%');
DECLARE @Penta int = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'Penta' OR VaccineName LIKE 'Pentavalent%');
DECLARE @OPV int   = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'OPV'   OR VaccineName LIKE 'Oral Polio%');
DECLARE @IPV int   = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'IPV'   OR VaccineName LIKE 'Inactivated Polio%');
DECLARE @PCV int   = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'PCV'   OR VaccineName LIKE 'Pneumococcal%');
DECLARE @MMR int   = (SELECT TOP 1 VaccineID FROM Vaccines WHERE Abbreviation = 'MMR'   OR VaccineName LIKE 'Measles%');

IF @BCG IS NULL OR @HepB IS NULL OR @Penta IS NULL OR @OPV IS NULL OR @IPV IS NULL OR @PCV IS NULL OR @MMR IS NULL
BEGIN
    RAISERROR('DemoSeed: one of the 7 EPI vaccines (BCG, HepB, Penta, OPV, IPV, PCV, MMR) is missing from dbo.Vaccines.', 16, 1);
    RETURN;
END

IF COL_LENGTH('dbo.VaccineInventory', 'ManufacturingDate') IS NULL OR OBJECT_ID('dbo.QueueQRCodes', 'U') IS NULL OR COL_LENGTH('dbo.Children', 'FamilyNo') IS NULL
BEGIN
    RAISERROR('DemoSeed: run Cleanup_2026-09.sql first (it adds the columns and tables this script fills).', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM VaccinationScheduleRules)
BEGIN
    RAISERROR('DemoSeed: dbo.VaccinationScheduleRules is empty — set up the schedule rules first.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

/* =====================================================================
   1. CLEAR PREVIOUS DEMO ACTIVITY (children-level data only)
   ===================================================================== */

DECLARE @DemoChild TABLE (ChildID uniqueidentifier PRIMARY KEY);
INSERT @DemoChild SELECT ChildID FROM Children WHERE CONVERT(char(36), ChildID) LIKE 'A2A30000-%';

DECLARE @DemoParent TABLE (ParentID uniqueidentifier PRIMARY KEY);
INSERT @DemoParent SELECT ParentID FROM Parents WHERE CONVERT(char(36), ParentID) LIKE 'A2A20000-%';

DECLARE @DemoUser TABLE (UserID uniqueidentifier PRIMARY KEY);
INSERT @DemoUser SELECT UserID FROM Users WHERE CONVERT(char(36), UserID) LIKE 'A2A10000-%';

UPDATE ClinicRooms SET CurrentChildID = NULL, IsOccupied = 0 WHERE CurrentChildID IN (SELECT ChildID FROM @DemoChild);

DELETE qc FROM QueueChildren qc
WHERE qc.ChildID IN (SELECT ChildID FROM @DemoChild)
   OR qc.QueueID IN (SELECT QueueID FROM Queues WHERE ParentID IN (SELECT ParentID FROM @DemoParent));
DELETE FROM Queues WHERE ParentID IN (SELECT ParentID FROM @DemoParent);

DELETE FROM Notifications
WHERE ChildID IN (SELECT ChildID FROM @DemoChild)
   OR ParentID IN (SELECT ParentID FROM @DemoParent)
   OR UserID IN (SELECT UserID FROM @DemoUser);

UPDATE VaccinationTimeline SET VaccinationRecordID = NULL WHERE ChildID IN (SELECT ChildID FROM @DemoChild);
DELETE FROM VaccinationRecords WHERE ChildID IN (SELECT ChildID FROM @DemoChild);
DELETE FROM VaccinationTimeline WHERE ChildID IN (SELECT ChildID FROM @DemoChild);
DELETE FROM ChildParentRelationship WHERE ChildID IN (SELECT ChildID FROM @DemoChild)
                                       OR CONVERT(char(36), RelationshipID) LIKE 'A2A50000-%';

-- Demo audit entries written by a previous run of this script
DELETE FROM AuditLogs WHERE ActionPerformed LIKE '%"Demo":true%';

-- demo.staff and demo.admin: the old "Staff" and "Administrator" levels,
-- replaced by Staff / Nurse and Admin / Doctor (older runs created them)
DECLARE @OldDemoUser TABLE (UserID uniqueidentifier PRIMARY KEY);
INSERT @OldDemoUser SELECT UserID FROM Users
WHERE UserID IN ('A2A10000-0000-0000-0000-000000000004', 'A2A10000-0000-0000-0000-000000000005');
UPDATE ClinicRooms SET AssignedDoctorID = NULL WHERE AssignedDoctorID IN (SELECT UserID FROM @OldDemoUser);
UPDATE VaccinationRecords SET AdministeredByUserID = NULL WHERE AdministeredByUserID IN (SELECT UserID FROM @OldDemoUser);
DELETE FROM AuditLogs WHERE UserID IN (SELECT UserID FROM @OldDemoUser);
DELETE FROM AccountOTPs WHERE AccountID IN (SELECT AccountID FROM Accounts WHERE ReferenceID IN (SELECT UserID FROM @OldDemoUser));
DELETE FROM Accounts WHERE ReferenceID IN (SELECT UserID FROM @OldDemoUser);
DELETE FROM Users WHERE UserID IN (SELECT UserID FROM @OldDemoUser);

/* =====================================================================
   2. PERSONNEL  (Users + Accounts)
   ===================================================================== */

DECLARE @Staff TABLE (
    UserID uniqueidentifier, AccountID uniqueidentifier, Username nvarchar(50),
    FirstName nvarchar(50), MiddleName nvarchar(50), LastName nvarchar(50),
    UserType nvarchar(20), Position nvarchar(20), PRCNo nvarchar(50),
    Email nvarchar(200), ContactNo nvarchar(20));

INSERT @Staff VALUES
 ('A2A10000-0000-0000-0000-000000000001','A2A40000-0000-0000-0000-000000000001','demo.doctor', N'Elena',       N'Marquez', N'Villanueva','Doctor',   'Doctor',        'PRC-0112233','elena.villanueva@demo.aruga.ph','09171230001'),
 ('A2A10000-0000-0000-0000-000000000002','A2A40000-0000-0000-0000-000000000002','demo.nurse',  N'Mark Anthony',N'Ramos',   N'Bautista',  'Nurse',    'Nurse',         'PRC-0445566','mark.bautista@demo.aruga.ph',   '09171230002'),
 ('A2A10000-0000-0000-0000-000000000003','A2A40000-0000-0000-0000-000000000003','demo.nurse2', N'Kristine Joy',N'Lopez',   N'Ramos',     'Nurse',    'Nurse',         'PRC-0778899','kristine.ramos@demo.aruga.ph',  '09171230003');

MERGE Users AS t
USING @Staff AS s ON t.UserID = s.UserID
WHEN MATCHED THEN UPDATE SET
    FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName,
    Username = s.Username, PasswordHash = @Pwd, ContactNo = s.ContactNo,
    UserType = s.UserType, Position = s.Position, PRCNo = s.PRCNo,
    AccountStatus = 'Active', Email = s.Email, Address = N'Leveriza St., Pasay City'
WHEN NOT MATCHED THEN INSERT
    (UserID, FirstName, MiddleName, LastName, Username, PasswordHash, ContactNo, UserType, PRCNo, AccountStatus, Email, Address, CreatedAt, Position)
    VALUES (s.UserID, s.FirstName, s.MiddleName, s.LastName, s.Username, @Pwd, s.ContactNo, s.UserType, s.PRCNo, 'Active', s.Email, N'Leveriza St., Pasay City', DATEADD(day, -45, @Now), s.Position);

MERGE Accounts AS t
USING @Staff AS s ON t.AccountID = s.AccountID
WHEN MATCHED THEN UPDATE SET
    Username = s.Username, PasswordHash = @Pwd, AccountType = 'Personnel', ReferenceID = s.UserID,
    Status = 1, MustChangePassword = 0, FailedLoginAttempts = 0, LockedUntil = NULL
WHEN NOT MATCHED THEN INSERT
    (AccountID, Username, PasswordHash, AccountType, ReferenceID, Status, MustChangePassword, FailedLoginAttempts, CreatedAt)
    VALUES (s.AccountID, s.Username, @Pwd, 'Personnel', s.UserID, 1, 0, 0, DATEADD(day, -45, @Now));

DECLARE @Doctor uniqueidentifier = 'A2A10000-0000-0000-0000-000000000001';
DECLARE @Nurse1 uniqueidentifier = 'A2A10000-0000-0000-0000-000000000002';
DECLARE @Nurse2 uniqueidentifier = 'A2A10000-0000-0000-0000-000000000003';

/* =====================================================================
   3. PARENTS / GUARDIANS  (Parents + Accounts, login = email)
   ===================================================================== */

DECLARE @Par TABLE (N int, ParentID uniqueidentifier, FirstName nvarchar(50), MiddleName nvarchar(50), LastName nvarchar(50), Email nvarchar(255), ContactNo nvarchar(20), BarangayNo nvarchar(20), Address nvarchar(255));
INSERT @Par VALUES
 ( 1,'A2A20000-0000-0000-0000-000000000001',N'Maria',   N'Dela Paz', N'Santos',   'maria.santos@demo.aruga.ph',    '09181110001','24',N'1122 Leveriza St., Pasay City'),
 ( 2,'A2A20000-0000-0000-0000-000000000002',N'Cardo',   N'Reyes',    N'Dalisay',  'cardo.dalisay@demo.aruga.ph',   '09181110002','21',N'45 Libertad St., Pasay City'),
 ( 3,'A2A20000-0000-0000-0000-000000000003',N'Liza',    N'Manalo',   N'Reyes',    'liza.reyes@demo.aruga.ph',      '09181110003','24',N'18 Dolores St., Pasay City'),
 ( 4,'A2A20000-0000-0000-0000-000000000004',N'Joel',    N'Santiago', N'Garcia',   'joel.garcia@demo.aruga.ph',     '09181110004','33',N'301 Protacio St., Pasay City'),
 ( 5,'A2A20000-0000-0000-0000-000000000005',N'Rowena',  N'Luna',     N'Cruz',     'rowena.cruz@demo.aruga.ph',     '09181110005','24',N'7 Leveriza St., Pasay City'),
 ( 6,'A2A20000-0000-0000-0000-000000000006',N'Dennis',  N'Abad',     N'Torres',   'dennis.torres@demo.aruga.ph',   '09181110006','19',N'88 Cuneta Ave., Pasay City'),
 ( 7,'A2A20000-0000-0000-0000-000000000007',N'Carmela', N'Rivera',   N'Villareal','carmela.villareal@demo.aruga.ph','09181110007','21',N'210 Leveriza St., Pasay City'),
 ( 8,'A2A20000-0000-0000-0000-000000000008',N'Arnel',   N'Ocampo',   N'Mercado',  'arnel.mercado@demo.aruga.ph',   '09181110008','33',N'5 Protacio St., Pasay City'),
 ( 9,'A2A20000-0000-0000-0000-000000000009',N'Jenny',   N'Tolentino',N'Navarro',  'jenny.navarro@demo.aruga.ph',   '09181110009','24',N'64 Taft Ave., Pasay City'),
 (10,'A2A20000-0000-0000-0000-000000000010',N'Paolo',   N'Sy',       N'Lim',      'paolo.lim@demo.aruga.ph',       '09181110010','19',N'12 Cuneta Ave., Pasay City'),
 (11,'A2A20000-0000-0000-0000-000000000011',N'Teresa',  N'Gomez',    N'Soriano',  'teresa.soriano@demo.aruga.ph',  '09181110011','21',N'140 Libertad St., Pasay City'),
 (12,'A2A20000-0000-0000-0000-000000000012',N'Ramil',   N'Castro',   N'Flores',   'ramil.flores@demo.aruga.ph',    '09181110012','33',N'77 Leveriza St., Pasay City'),
 (13,'A2A20000-0000-0000-0000-000000000013',N'Kristel', N'Aquino',   N'Castillo', 'kristel.castillo@demo.aruga.ph','09181110013','24',N'9 Harrison St., Pasay City'),
 -- Isabela Cruz's grandmother (see the extra guardian link in section 4)
 (14,'A2A20000-0000-0000-0000-000000000014',N'Lourdes', N'Santos',   N'Luna',     'lourdes.luna@demo.aruga.ph',    '09181110014','24',N'7 Leveriza St., Pasay City');

MERGE Parents AS t
USING @Par AS s ON t.ParentID = s.ParentID
WHEN MATCHED THEN UPDATE SET
    FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName, Email = s.Email,
    ContactNo = s.ContactNo, BarangayNo = s.BarangayNo, Address = s.Address, PasswordHash = @Pwd,
    MustChangePassword = 0, TemporaryPasswordExpiresAt = NULL, UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ParentID, FirstName, MiddleName, LastName, Email, ContactNo, BarangayNo, Address, CreatedAt, UpdatedAt, PasswordHash, MustChangePassword)
    VALUES (s.ParentID, s.FirstName, s.MiddleName, s.LastName, s.Email, s.ContactNo, s.BarangayNo, s.Address, DATEADD(day, -30 - s.N * 20, @Now), @Now, @Pwd, 0);

MERGE Accounts AS t
USING (SELECT ParentID, Email, N FROM @Par) AS s
   ON t.AccountID = CAST('A2A40000-0000-0000-0001-' + RIGHT('000000000000' + CAST(s.N AS varchar(12)), 12) AS uniqueidentifier)
WHEN MATCHED THEN UPDATE SET
    Username = s.Email, PasswordHash = @Pwd, AccountType = 'Parent', ReferenceID = s.ParentID,
    Status = 1, MustChangePassword = 0, FailedLoginAttempts = 0, LockedUntil = NULL
WHEN NOT MATCHED THEN INSERT
    (AccountID, Username, PasswordHash, AccountType, ReferenceID, Status, MustChangePassword, FailedLoginAttempts, CreatedAt)
    VALUES (CAST('A2A40000-0000-0000-0001-' + RIGHT('000000000000' + CAST(s.N AS varchar(12)), 12) AS uniqueidentifier),
            s.Email, @Pwd, 'Parent', s.ParentID, 1, 0, 0, DATEADD(day, -30 - s.N * 20, @Now));

/* =====================================================================
   4. CHILDREN
   AgeDays is chosen so the group covers every situation a health worker
   sees: newborns, doses due TODAY, on-track, fully vaccinated, and
   children who MISSED doses (MissedFromAge = first missed dose's age).
   DoneToday = their doses due today were already given this morning.
   ===================================================================== */

DECLARE @Kid TABLE (N int, ChildID uniqueidentifier, ParentN int, Relationship nvarchar(20),
    FirstName nvarchar(50), MiddleName nvarchar(50), LastName nvarchar(50), Sex nvarchar(10),
    AgeDays int, BirthWeight decimal(5,2), BirthHeight decimal(5,2), Allergies nvarchar(200),
    MissedFromAge int NULL, DoneToday bit);

INSERT @Kid VALUES
 ( 1,'A2A30000-0000-0000-0000-000000000001', 1,'Mother',N'Kyle',     N'Dela Paz', N'Santos',   'Male',     0, 3.20, 50.0, NULL,            NULL, 0), -- newborn: BCG + HepB due today
 ( 2,'A2A30000-0000-0000-0000-000000000002', 2,'Father',N'Sofia',    N'Reyes',    N'Dalisay',  'Female',  42, 2.95, 48.5, NULL,            NULL, 1), -- 6 weeks: given today
 ( 3,'A2A30000-0000-0000-0000-000000000003', 2,'Father',N'Ethan',    N'Reyes',    N'Dalisay',  'Male',    70, 3.40, 51.0, NULL,            NULL, 1), -- 10 weeks: given today
 ( 4,'A2A30000-0000-0000-0000-000000000004', 3,'Mother',N'Mia',      N'Manalo',   N'Reyes',    'Female',  98, 3.10, 49.0, N'None known',   NULL, 0), -- 14 weeks: due today
 ( 5,'A2A30000-0000-0000-0000-000000000005', 4,'Father',N'Lucas',    N'Santiago', N'Garcia',   'Male',   270, 3.55, 52.0, NULL,            NULL, 0), -- 9 months: MMR 1 due today
 ( 6,'A2A30000-0000-0000-0000-000000000006', 5,'Mother',N'Isabela',  N'Luna',     N'Cruz',     'Female', 120, 3.00, 49.5, NULL,            NULL, 0), -- on track
 ( 7,'A2A30000-0000-0000-0000-000000000007', 6,'Father',N'Gabriel',  N'Abad',     N'Torres',   'Male',   200, 3.60, 51.5, N'Egg (mild)',   NULL, 0), -- on track
 ( 8,'A2A30000-0000-0000-0000-000000000008', 7,'Mother',N'Andrea',   N'Rivera',   N'Villareal','Female', 420, 2.85, 48.0, NULL,            NULL, 0), -- fully vaccinated
 ( 9,'A2A30000-0000-0000-0000-000000000009', 8,'Father',N'Nathan',   N'Ocampo',   N'Mercado',  'Male',   560, 3.25, 50.5, NULL,            NULL, 0), -- fully vaccinated
 (10,'A2A30000-0000-0000-0000-000000000010', 9,'Mother',N'Chloe',    N'Tolentino',N'Navarro',  'Female', 150, 3.05, 49.0, NULL,              98, 0), -- missed 14-week doses
 (11,'A2A30000-0000-0000-0000-000000000011',10,'Father',N'Joshua',   N'Sy',       N'Lim',      'Male',    60, 3.30, 50.0, NULL,              42, 0), -- missed 6-week doses
 (12,'A2A30000-0000-0000-0000-000000000012',11,'Mother',N'Angela',   N'Gomez',    N'Soriano',  'Female',  20, 3.15, 49.0, NULL,            NULL, 0), -- upcoming (6 weeks)
 (13,'A2A30000-0000-0000-0000-000000000013',11,'Mother',N'Miguel',   N'Gomez',    N'Soriano',  'Male',    35, 3.45, 51.0, NULL,            NULL, 0), -- upcoming in a week
 (14,'A2A30000-0000-0000-0000-000000000014',12,'Father',N'Ella',     N'Castro',   N'Flores',   'Female',  66, 2.90, 48.5, NULL,            NULL, 0), -- dose 2 in a few days
 (15,'A2A30000-0000-0000-0000-000000000015',12,'Father',N'Liam',     N'Castro',   N'Flores',   'Male',   260, 3.50, 51.5, N'Penicillin',   NULL, 0), -- MMR in ~10 days
 (16,'A2A30000-0000-0000-0000-000000000016',13,'Mother',N'Zoe',      N'Aquino',   N'Castillo', 'Female', 360, 3.00, 49.0, NULL,            NULL, 0), -- MMR 2 in a few days
 (17,'A2A30000-0000-0000-0000-000000000017',13,'Mother',N'Noah',     N'Aquino',   N'Castillo', 'Male',   800, 3.35, 50.5, NULL,            NULL, 0), -- fully vaccinated
 (18,'A2A30000-0000-0000-0000-000000000018', 9,'Mother',N'Hannah',   N'Tolentino',N'Navarro',  'Female', 130, 3.10, 49.5, NULL,              98, 0); -- missed 14-week doses

MERGE Children AS t
USING (SELECT k.*, p.Address, p.BarangayNo FROM @Kid k JOIN @Par p ON p.N = k.ParentN) AS s ON t.ChildID = s.ChildID
WHEN MATCHED THEN UPDATE SET
    FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName,
    BirthDate = DATEADD(day, -s.AgeDays, @Today), PlaceOfBirth = N'Pasay City General Hospital',
    Address = LEFT(s.Address, 70), HealthCenter = N'Leveriza Health Center', Barangay = TRY_CAST(s.BarangayNo AS int),
    FamilyNo = N'FN-' + RIGHT('000' + CAST(s.ParentN AS varchar(3)), 3),
    Sex = s.Sex, Allergies = s.Allergies, BirthWeight = s.BirthWeight, BirthHeight = s.BirthHeight, UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (ChildID, FirstName, MiddleName, LastName, BirthDate, PlaceOfBirth, Address, HealthCenter, Barangay, FamilyNo, Sex, CreatedAt, UpdatedAt, Allergies, BirthHeight, BirthWeight)
    VALUES (s.ChildID, s.FirstName, s.MiddleName, s.LastName, DATEADD(day, -s.AgeDays, @Today), N'Pasay City General Hospital',
            LEFT(s.Address, 70), N'Leveriza Health Center', TRY_CAST(s.BarangayNo AS int), N'FN-' + RIGHT('000' + CAST(s.ParentN AS varchar(3)), 3), s.Sex,
            DATEADD(day, -s.AgeDays, @Now), @Now, s.Allergies, s.BirthHeight, s.BirthWeight);

-- Children registered at birth: CreatedAt = birth date (keeps "new patients" stats sensible)
UPDATE c SET CreatedAt = DATEADD(hour, 10, CAST(DATEADD(day, -k.AgeDays, @Today) AS datetime2))
FROM Children c JOIN @Kid k ON k.ChildID = c.ChildID;

INSERT ChildParentRelationship (RelationshipID, ChildID, ParentID, RelationshipType, IsPrimaryContact, CanReceiveNotifications, Status, CreatedAt)
SELECT CAST('A2A50000-0000-0000-0000-' + RIGHT('000000000000' + CAST(k.N AS varchar(12)), 12) AS uniqueidentifier),
       k.ChildID, p.ParentID, k.Relationship, 1, 1, 'Active', @Now
FROM @Kid k JOIN @Par p ON p.N = k.ParentN;

-- A second guardian who isn't a parent: Isabela's grandmother often brings
-- her in, so the staff linked her as "Grandmother". She has her own login and
-- can check Isabela in; staff and health workers then see "(Grandmother)".
INSERT ChildParentRelationship (RelationshipID, ChildID, ParentID, RelationshipType, IsPrimaryContact, CanReceiveNotifications, Status, CreatedAt)
VALUES ('A2A50000-0000-0000-0001-000000000006', 'A2A30000-0000-0000-0000-000000000006',
        'A2A20000-0000-0000-0000-000000000014', 'Grandmother', 0, 1, 'Active', @Now);

/* =====================================================================
   5. VACCINE INVENTORY  (upserted by lot number, never deleted)
   ===================================================================== */

DECLARE @Inv TABLE (LotNumber nvarchar(100), VaccineID int, TargetLeft int, MinimumStock int, ExpiresIn int, ReceivedAgo int, Supplier nvarchar(255), IsMain bit);
INSERT @Inv VALUES
 ('DEMO-BCG-2604',   @BCG,   64, 20, 300, 120, N'DOH – Center for Health Development NCR', 1),
 ('DEMO-HEPB-2603',  @HepB,  57, 20, 250, 110, N'DOH – Center for Health Development NCR', 1),
 ('DEMO-HEPB-2509',  @HepB,  15, 20, -10, 300, N'DOH – Center for Health Development NCR', 0), -- expired batch
 ('DEMO-PENTA-2605', @Penta,118, 30, 400,  90, N'DOH – Center for Health Development NCR', 1),
 ('DEMO-OPV-2602',   @OPV,  121, 30, 200, 130, N'DOH – Center for Health Development NCR', 1),
 ('DEMO-IPV-2601',   @IPV,   18, 30, 190, 150, N'DOH – Center for Health Development NCR', 1), -- LOW stock
 ('DEMO-PCV-2606',   @PCV,   96, 30, 420,  80, N'DOH – Center for Health Development NCR', 1),
 ('DEMO-PCV-2511',   @PCV,   12, 10,  20, 260, N'DOH – Center for Health Development NCR', 0), -- expiring in 20 days
 ('DEMO-MMR-2604',   @MMR,   45, 20, 180, 100, N'DOH – Center for Health Development NCR', 1);

MERGE VaccineInventory AS t
USING @Inv AS s ON t.LotNumber = s.LotNumber
WHEN MATCHED THEN UPDATE SET
    VaccineID = s.VaccineID, MinimumStock = s.MinimumStock, ExpirationDate = DATEADD(day, s.ExpiresIn, @Today),
    ManufacturingDate = DATEADD(day, s.ExpiresIn - 730, @Today),
    ReceivedDate = DATEADD(day, -s.ReceivedAgo, @Today), Supplier = s.Supplier, Status = 1, UpdatedAt = @Now
WHEN NOT MATCHED THEN INSERT
    (VaccineID, LotNumber, InitialQuantity, CurrentQuantity, MinimumStock, ExpirationDate, ManufacturingDate, ReceivedDate, Supplier, Status, CreatedAt)
    VALUES (s.VaccineID, s.LotNumber, s.TargetLeft, s.TargetLeft, s.MinimumStock, DATEADD(day, s.ExpiresIn, @Today), DATEADD(day, s.ExpiresIn - 730, @Today),
            DATEADD(day, -s.ReceivedAgo, @Today), s.Supplier, 1, DATEADD(day, -s.ReceivedAgo, @Now));

/* =====================================================================
   6. VACCINATION TIMELINE — same rule the backend uses:
      expected = birth + RecommendedAgeDays, scheduled = next open clinic day
   ===================================================================== */

DECLARE @TL TABLE (TimelineID uniqueidentifier, ChildN int, ChildID uniqueidentifier, VaccineID int, DoseNumber int,
                   RecommendedAgeDays int, ExpectedDate date, ScheduledDate date, Seq int);

INSERT @TL
SELECT NEWID(), k.N, k.ChildID, r.VaccineID, r.DoseNumber, r.RecommendedAgeDays,
       e.ExpectedDate, ISNULL(sd.d, e.ExpectedDate), r.SequenceOrder
FROM @Kid k
CROSS JOIN VaccinationScheduleRules r
CROSS APPLY (SELECT DATEADD(day, r.RecommendedAgeDays, DATEADD(day, -k.AgeDays, @Today)) AS ExpectedDate) e
OUTER APPLY (
    SELECT TOP 1 DATEADD(day, n.n, e.ExpectedDate) AS d
    FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) n(n)
    WHERE 1 = COALESCE(
        (SELECT TOP 1 CAST(x.IsOpen AS int) FROM ClinicScheduleExceptions x
          WHERE x.IsActive = 1 AND x.ExceptionDate = DATEADD(day, n.n, e.ExpectedDate)),
        (SELECT TOP 1 CAST(s.IsOpen AS int) FROM ClinicOperatingSchedule s
          WHERE s.IsActive = 1 AND s.DayOfWeek = DATEDIFF(day, '19000107', DATEADD(day, n.n, e.ExpectedDate)) % 7),
        0)
    ORDER BY n.n
) sd;

-- Which doses were given, and when
DECLARE @Given TABLE (TimelineID uniqueidentifier, GivenOn datetime2, RowN int);
INSERT @Given
SELECT t.TimelineID,
       CASE WHEN t.ScheduledDate >= @Today
            THEN DATEADD(minute, 8 * 60 + 20 + (ROW_NUMBER() OVER (ORDER BY t.ChildN, t.Seq) * 11) % 150, CAST(@Today AS datetime2))
            ELSE DATEADD(minute, 8 * 60 + (ROW_NUMBER() OVER (ORDER BY t.ChildN, t.Seq) * 17) % 210, CAST(t.ScheduledDate AS datetime2)) END,
       ROW_NUMBER() OVER (ORDER BY t.ScheduledDate, t.ChildN, t.Seq)
FROM @TL t JOIN @Kid k ON k.N = t.ChildN
WHERE (t.ScheduledDate < @Today AND (k.MissedFromAge IS NULL OR t.RecommendedAgeDays < k.MissedFromAge))
   OR (t.ScheduledDate = @Today AND k.DoneToday = 1);

DECLARE @MainInv TABLE (VaccineID int PRIMARY KEY, InventoryID int);
INSERT @MainInv SELECT i.VaccineID, v.InventoryID
FROM @Inv i JOIN VaccineInventory v ON v.LotNumber = i.LotNumber WHERE i.IsMain = 1;

-- Timeline rows (records are linked right after)
INSERT VaccinationTimeline (TimelineID, TimelineCode, ChildID, VaccineID, DoseNumber, ExpectedDate, ScheduledDate, CompletedDate, VaccinationRecordID, Status, CreatedAt)
SELECT t.TimelineID,
       'TL-DEMO-' + RIGHT('00' + CAST(t.ChildN AS varchar(3)), 2) + '-' + CAST(t.VaccineID AS varchar(5)) + '-' + CAST(t.DoseNumber AS varchar(3)),
       t.ChildID, t.VaccineID, t.DoseNumber, t.ExpectedDate, t.ScheduledDate,
       CAST(g.GivenOn AS date), NULL,
       CASE WHEN g.TimelineID IS NOT NULL THEN 'Completed'
            WHEN t.ScheduledDate < @Today THEN 'Missed'
            ELSE 'Pending' END,
       DATEADD(day, -1 * (SELECT AgeDays FROM @Kid WHERE N = t.ChildN), @Now)
FROM @TL t LEFT JOIN @Given g ON g.TimelineID = t.TimelineID;

-- Administered doses
INSERT VaccinationRecords (VaccinationRecordID, RecordCode, ChildID, VaccineID, InventoryID, TimelineID, DoseNumber, VaccinationDate,
                           AdministeredByUserID, NurseObservation, Status, CreatedAt)
SELECT NEWID(),
       'VR-DEMO-' + RIGHT('00000' + CAST(g.RowN AS varchar(6)), 5),
       t.ChildID, t.VaccineID, mi.InventoryID, t.TimelineID, t.DoseNumber, g.GivenOn,
       CASE g.RowN % 2 WHEN 0 THEN @Nurse1 ELSE @Nurse2 END,   -- the Nurses vaccinate
       CHOOSE(g.RowN % 7 + 1,
              N'No adverse reaction observed.',
              N'Mild redness and swelling at the injection site.',
              N'Low-grade fever reported the same evening; advised paracetamol and monitoring.',
              N'Child was fussy for a few minutes, settled after feeding.',
              NULL,
              N'No adverse reaction observed. Parent advised on next schedule.',
              N'Observed for 30 minutes after injection — no reaction.'),
       'Completed', g.GivenOn
FROM @Given g
JOIN @TL t ON t.TimelineID = g.TimelineID
JOIN @MainInv mi ON mi.VaccineID = t.VaccineID;

-- Where each demo dose was given (typical sites; the Nurse picks the real
-- one when recording). The column is added here too in case the API hasn't
-- started on this database yet; EXEC because it may be new in this batch.
IF COL_LENGTH('dbo.VaccinationRecords', 'InjectionSite') IS NULL
    ALTER TABLE dbo.VaccinationRecords ADD InjectionSite NVARCHAR(30) NULL;
EXEC (N'UPDATE vr SET InjectionSite = CASE v.Abbreviation
            WHEN ''BCG''   THEN N''Right upper arm''
            WHEN ''MMR''   THEN N''Left upper arm''
            WHEN ''OPV''   THEN N''Mouth (oral)''
            WHEN ''Penta'' THEN N''Left thigh''
            ELSE N''Right thigh'' END
        FROM VaccinationRecords vr JOIN Vaccines v ON v.VaccineID = vr.VaccineID
        WHERE CONVERT(char(36), vr.ChildID) LIKE ''A2A30000-%''');

-- OPV is given by mouth: no injection-site remarks on it
UPDATE vr SET NurseObservation = N'No adverse reaction observed.'
FROM VaccinationRecords vr JOIN Vaccines v ON v.VaccineID = vr.VaccineID
WHERE v.Abbreviation = 'OPV' AND vr.ChildID IN (SELECT ChildID FROM @Kid)
  AND (vr.NurseObservation LIKE N'%injection%' OR vr.NurseObservation LIKE N'%swelling%');

UPDATE vt SET VaccinationRecordID = vr.VaccinationRecordID
FROM VaccinationTimeline vt JOIN VaccinationRecords vr ON vr.TimelineID = vt.TimelineID
WHERE vt.ChildID IN (SELECT ChildID FROM @Kid);

-- Stock left = what each batch should show after the doses above were used
UPDATE v SET
    InitialQuantity = i.TargetLeft + ISNULL(u.Used, 0),
    CurrentQuantity = i.TargetLeft,
    UpdatedAt = @Now
FROM VaccineInventory v
JOIN @Inv i ON i.LotNumber = v.LotNumber
OUTER APPLY (SELECT COUNT(*) AS Used FROM VaccinationRecords r WHERE r.InventoryID = v.InventoryID) u;

/* =====================================================================
   7. VACCINATION ROOM
   Leveriza has one vaccination room and one person vaccinating: the
   Nurse calls each family in with Call Next, so there are no stations
   to set up. (Older versions of this script put the demo health workers
   in Room 1 / 2 / 3; that is cleared here.)
   ===================================================================== */

UPDATE ClinicRooms SET AssignedDoctorID = NULL
WHERE AssignedDoctorID IN (@Doctor, @Nurse1, @Nurse2) AND IsOccupied = 0;

/* =====================================================================
   8. TODAY'S QUEUE
   #1 Dalisay family — finished this morning
   #2 Reyes — inside the vaccination room right now (called by Call Next)
   #3 Santos (newborn), #4 Garcia, #5 Navarro (catch-up) — waiting for
      the Nurse to call them in
   ===================================================================== */

DECLARE @QBase int = ISNULL((SELECT MAX(QueueNumber) FROM Queues WHERE QueueDate = @Today), 0);

DECLARE @Q TABLE (N int, QueueID uniqueidentifier, ParentN int, Status nvarchar(20), MinutesAgo int);
INSERT @Q VALUES
 (1,'A2A60000-0000-0000-0000-000000000001', 2,'Completed', 150),
 (2,'A2A60000-0000-0000-0000-000000000002', 3,'InProgress',100),
 (3,'A2A60000-0000-0000-0000-000000000003', 1,'Waiting',    70),
 (4,'A2A60000-0000-0000-0000-000000000004', 4,'Waiting',    45),
 (5,'A2A60000-0000-0000-0000-000000000005', 9,'Waiting',    20);

INSERT Queues (QueueID, ParentID, QueueNumber, QueueDate, Status, AssignedRoomID, CreatedAt, UpdatedAt)
SELECT q.QueueID, p.ParentID, @QBase + q.N, @Today, q.Status,
       NULL,
       DATEADD(minute, -q.MinutesAgo, @Now),
       CASE WHEN q.Status <> 'Waiting' THEN DATEADD(minute, -q.MinutesAgo + 25, @Now) END
FROM @Q q JOIN @Par p ON p.N = q.ParentN;

-- Queue #5 is a catch-up visit for Chloe only (Hannah stays home)
INSERT QueueChildren (QueueChildID, QueueID, ChildID, CreatedAt)
SELECT CAST('A2A70000-0000-0000-0000-' + RIGHT('000000000000' + CAST(k.N AS varchar(12)), 12) AS uniqueidentifier),
       q.QueueID, k.ChildID, DATEADD(minute, -q.MinutesAgo, @Now)
FROM @Q q JOIN @Kid k ON k.ParentN = q.ParentN
WHERE NOT (q.N = 5 AND k.N = 18);

/* =====================================================================
   9. NOTIFICATIONS
   (vaccine reminders / overdue follow-ups are generated automatically by
    the backend's daily NotificationGeneratorService when the API starts)
   ===================================================================== */

-- "Vaccine administered" notices for every dose given in the last 21 days
INSERT Notifications (NotificationID, ParentID, ChildID, VaccineID, DoseNumber, Type, Title, Message, ScheduledDate, IsRead, CreatedAt)
SELECT NEWID(), p.ParentID, k.ChildID, vr.VaccineID, vr.DoseNumber, 'Completed',
       N'Vaccine administered — ' + k.FirstName + N' ' + k.LastName,
       v.VaccineName + N' (Dose ' + CAST(vr.DoseNumber AS nvarchar(5)) + N') was administered to ' + k.FirstName + N' ' + k.LastName +
       N' on ' + FORMAT(vr.VaccinationDate, 'MMMM d, yyyy') + N'.',
       CAST(vr.VaccinationDate AS date),
       CASE WHEN vr.VaccinationDate < DATEADD(day, -3, @Today) THEN 1 ELSE 0 END,
       DATEADD(minute, 5, vr.VaccinationDate)
FROM VaccinationRecords vr
JOIN @Kid k ON k.ChildID = vr.ChildID
JOIN @Par p ON p.N = k.ParentN
JOIN Vaccines v ON v.VaccineID = vr.VaccineID
WHERE vr.VaccinationDate >= DATEADD(day, -21, @Today);

-- Clinic announcement to every demo parent
INSERT Notifications (NotificationID, ParentID, Type, Title, Message, IsRead, CreatedAt)
SELECT NEWID(), p.ParentID, 'Announcement',
       N'Measles-Rubella catch-up week',
       N'Leveriza Health Center will hold a catch-up vaccination week starting next Monday, 8:00 AM – 12:00 PM. ' +
       N'Children with missed doses are encouraged to come. Please bring your child''s Yellow Book.',
       CASE WHEN p.N % 3 = 0 THEN 1 ELSE 0 END, DATEADD(day, -2, @Now)
FROM @Par p;

-- Low-stock bell alert for the Doctor and Nurses
INSERT Notifications (NotificationID, UserID, VaccineID, Type, Title, Message, IsRead, CreatedAt)
SELECT NEWID(), u.UserID, @IPV, 'LowStock',
       N'Low stock — Inactivated Polio Vaccine',
       N'Batch DEMO-IPV-2601 of Inactivated Polio Vaccine is down to 18 dose(s), below the minimum of 30. Please request a restock.',
       0, DATEADD(hour, -20, @Now)
FROM (VALUES (@Doctor), (@Nurse1), (@Nurse2)) u(UserID);

/* =====================================================================
   10. AUDIT TRAIL (so the admin Audit Logs page has a realistic history)
   ===================================================================== */

DECLARE @A TABLE (MinutesAgo int, UserID uniqueidentifier, UserName nvarchar(100), Role nvarchar(40), Module nvarchar(50), Action nvarchar(50),
                  AffectedRecord nvarchar(200), Description nvarchar(400), Status nvarchar(20), Ip nvarchar(40), Device nvarchar(60),
                  OldValue nvarchar(200), NewValue nvarchar(200));

INSERT @A VALUES
 (60*24*6+300, @Doctor, NULL, 'SystemAdmin', 'Authentication',    'Login',            N'Account – demo.doctor',            'Signed in successfully.',                                         'Success','192.168.1.10','Chrome on Windows', NULL, NULL),
 (60*24*6+290, @Doctor, NULL, 'SystemAdmin', 'User Management',   'Create',           N'Nurse Account – Kristine Joy Ramos','Created a new Nurse account (username demo.nurse2).',               'Success','192.168.1.10','Chrome on Windows', NULL, 'Status: Active, Role: Nurse'),
 (60*24*6+280, @Doctor, NULL, 'SystemAdmin', 'Inventory',         'Receive Stock',    N'Batch DEMO-MMR-2604',              'Received a new vaccine batch of 80 dose(s).',                    'Success','192.168.1.10','Chrome on Windows', NULL, 'Remaining: 80'),
 (60*24*5+200, @Nurse2, NULL, 'Staff',       'Authentication',    'Login',            N'Account – demo.nurse2',            'Signed in successfully.',                                         'Success','192.168.1.21','Chrome on Windows', NULL, NULL),
 (60*24*5+190, @Nurse2, NULL, 'Staff',       'Patient Management','Create',           N'Child – Kyle Santos',              'Registered a new child and generated the vaccination timeline.',  'Success','192.168.1.21','Chrome on Windows', NULL, NULL),
 (60*24*4+400, NULL, N'Unknown', 'Parent',   'Authentication',    'Login',            N'Account – jenny.navaro@demo.aruga.ph','Login attempt with an unknown username or email.',            'Failed', '203.177.42.9','Chrome on Android', NULL, NULL),
 (60*24*4+395, NULL, N'Jenny Navarro','Parent','Authentication',  'Login',            N'Account – jenny.navarro@demo.aruga.ph','Signed in successfully.',                                     'Success','203.177.42.9','Chrome on Android', NULL, NULL),
 (60*24*3+240, @Nurse1, NULL, 'Staff',       'Authentication',    'Login',            N'Account – demo.nurse',             'Signed in successfully.',                                         'Success','192.168.1.31','Safari on iOS', NULL, NULL),
 (60*24*3+120, @Doctor, NULL, 'SystemAdmin', 'Inventory',         'Adjust Inventory', N'Batch DEMO-HEPB-2509',             'Flagged expired batch — kept for records, not for use.',         'Warning','192.168.1.10','Chrome on Windows', 'Remaining: 15, Active: True', 'Remaining: 15, Active: True'),
 (60*24*2+300, @Doctor, NULL, 'SystemAdmin', 'Notifications',     'Create',           N'Announcement – Measles-Rubella catch-up week','Sent an announcement to 13 parent(s).',           'Success','192.168.1.10','Chrome on Windows', NULL, NULL),
 (60*24*1+200, @Doctor, NULL, 'SystemAdmin', 'Authentication',    'Login',            N'Account – demo.doctor',            'Signed in successfully.',                                         'Success','192.168.1.33','Chrome on Windows', NULL, NULL),
 (60*24*1+180, @Doctor, NULL, 'SystemAdmin', 'User Management',   'Update',           N'Profile – Elena Villanueva',       'Updated own profile information.',                                'Success','192.168.1.33','Chrome on Windows', NULL, NULL),
 (200,         @Nurse2, NULL, 'Staff',       'Authentication',    'Login',            N'Account – demo.nurse2',            'Signed in successfully.',                                         'Success','192.168.1.21','Chrome on Windows', NULL, NULL),
 (180,         @Nurse1, NULL, 'Staff',       'Authentication',    'Login',            N'Account – demo.nurse',             'Signed in successfully.',                                         'Success','192.168.1.31','Safari on iOS', NULL, NULL);

-- One "Vaccinate Child" entry per dose given in the last 7 days
INSERT @A
SELECT DATEDIFF(minute, vr.VaccinationDate, @Now), vr.AdministeredByUserID, NULL, CASE WHEN vr.AdministeredByUserID = @Doctor THEN 'SystemAdmin' ELSE 'Staff' END, 'Vaccination', 'Vaccinate Child',
       k.FirstName + N' ' + k.LastName + N' – ' + v.VaccineName + N' Dose ' + CAST(vr.DoseNumber AS nvarchar(5)),
       ISNULL(N'Recorded an administered vaccine dose. Remarks: ' + vr.NurseObservation, N'Recorded an administered vaccine dose.'),
       'Success', '192.168.1.3' + CAST(vr.DoseNumber AS nvarchar(2)), 'Chrome on Windows', NULL, N'Record ' + vr.RecordCode
FROM VaccinationRecords vr
JOIN @Kid k ON k.ChildID = vr.ChildID
JOIN Vaccines v ON v.VaccineID = vr.VaccineID
WHERE vr.VaccinationDate >= DATEADD(day, -7, @Today) AND vr.VaccinationDate <= @Now;

INSERT AuditLogs (UserID, ActionPerformed, ActionDate)
SELECT a.UserID,
       (SELECT a.Module AS Module, a.Action AS Action, a.AffectedRecord AS AffectedRecord, a.Description AS Description,
               a.Status AS Status, a.UserName AS UserName, a.Role AS Role, a.Ip AS IpAddress, a.Device AS Device,
               a.OldValue AS OldValue, a.NewValue AS NewValue, CAST(1 AS bit) AS Demo
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
       DATEADD(minute, -a.MinutesAgo, @Now)
FROM @A a
ORDER BY a.MinutesAgo DESC;

COMMIT TRANSACTION;

/* ---------------------------------------------------------------------
   Summary
   --------------------------------------------------------------------- */
SELECT
    (SELECT COUNT(*) FROM Children WHERE CONVERT(char(36), ChildID) LIKE 'A2A30000-%')                 AS DemoChildren,
    (SELECT COUNT(*) FROM Parents WHERE CONVERT(char(36), ParentID) LIKE 'A2A20000-%')                 AS DemoParents,
    (SELECT COUNT(*) FROM VaccinationRecords WHERE CONVERT(char(36), ChildID) LIKE 'A2A30000-%')       AS DosesGiven,
    (SELECT COUNT(*) FROM VaccinationTimeline WHERE CONVERT(char(36), ChildID) LIKE 'A2A30000-%'
        AND Status = 'Pending' AND ScheduledDate = @Today)                                              AS DueToday,
    (SELECT COUNT(*) FROM VaccinationTimeline WHERE CONVERT(char(36), ChildID) LIKE 'A2A30000-%'
        AND Status = 'Missed')                                                                          AS Overdue,
    (SELECT COUNT(*) FROM Queues WHERE CONVERT(char(36), ParentID) LIKE 'A2A20000-%' AND QueueDate = @Today) AS QueueToday;

PRINT 'Aruga demo data loaded. Log in with any demo account — password: Aruga@2026';

-- Parents can only check in on a vaccination day, during check-in hours.
IF 1 <> COALESCE(
    (SELECT TOP 1 CAST(IsOpen AS int) FROM ClinicScheduleExceptions WHERE IsActive = 1 AND ExceptionDate = @Today),
    (SELECT TOP 1 CAST(IsOpen AS int) FROM ClinicOperatingSchedule
      WHERE IsActive = 1 AND DayOfWeek = DATEDIFF(day, '19000107', @Today) % 7), 0)
    PRINT 'NOTE: today is not a vaccination day, so check-in is closed and nothing is due today. '
        + 'For a demo, add today under System Admin > Operating Hours > Add Exception (open), then run this script again.';
