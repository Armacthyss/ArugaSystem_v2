/* =====================================================================
   ARUGA SYSTEM — UPDATED DATABASE SCHEMA
   =====================================================================
   This script creates the complete database schema for ArugaSystemDB.
   It includes all tables, columns, constraints, indexes, and relationships.

   NO DATA is inserted by this script — use DemoSeed.sql for demo data.

   Created: 2026-09-30
   ===================================================================== */

-- Check if database exists; create if not
IF DB_ID(N'ArugaSystemDB') IS NULL
    CREATE DATABASE ArugaSystemDB;
GO

USE ArugaSystemDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* =====================================================================
   USERS AND AUTHENTICATION
   ===================================================================== */

-- System users: doctors, nurses, staff, administrators
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
CREATE TABLE dbo.Users (
    UserID           uniqueidentifier NOT NULL CONSTRAINT DF_Users_UserID DEFAULT (NEWID()),
    FirstName        nvarchar(50)     NULL,
    MiddleName       nvarchar(50)     NULL,
    LastName         nvarchar(50)     NULL,
    Username         nvarchar(50)     NOT NULL UNIQUE,
    PasswordHash     nvarchar(max)    NOT NULL,
    Email            nvarchar(200)    NULL,
    ContactNo        nvarchar(20)     NULL,
    Address          nvarchar(500)    NULL,
    UserType         nvarchar(20)     NOT NULL DEFAULT 'Healthcare',
    Position         nvarchar(50)     NOT NULL,  -- Doctor / Nurse / Staff / Administrator
    PRCNo            nvarchar(50)     NULL,      -- Professional license number
    AccountStatus    nvarchar(20)     NOT NULL DEFAULT 'Active',
    CreatedAt        datetime         NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_Users PRIMARY KEY (UserID)
);

-- Healthcare personnel (doctors, nurses, staff)
IF OBJECT_ID(N'dbo.Personnel', N'U') IS NULL
CREATE TABLE dbo.Personnel (
    PersonnelID uniqueidentifier NOT NULL CONSTRAINT DF_Personnel_PersonnelID DEFAULT (NEWID()),
    FirstName   nvarchar(100)    NOT NULL,
    MiddleName  nvarchar(100)    NULL,
    LastName    nvarchar(100)    NOT NULL,
    Role        nvarchar(20)     NOT NULL,  -- Doctor / Nurse / Staff / Administrator
    ContactNo   nvarchar(20)     NOT NULL,
    Email       nvarchar(255)    NULL,
    Address     nvarchar(500)    NULL,
    CreatedAt   datetime2        NOT NULL CONSTRAINT DF_Personnel_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt   datetime2        NULL,
    CONSTRAINT PK_Personnel PRIMARY KEY (PersonnelID)
);

-- Parent / Guardian accounts
IF OBJECT_ID(N'dbo.Parents', N'U') IS NULL
CREATE TABLE dbo.Parents (
    ParentID                   uniqueidentifier NOT NULL CONSTRAINT DF_Parents_ParentID DEFAULT (NEWID()),
    FirstName                  nvarchar(50)     NOT NULL,
    MiddleName                 nvarchar(50)     NULL,
    LastName                   nvarchar(50)     NOT NULL,
    Email                      nvarchar(255)    UNIQUE,
    ContactNo                  nvarchar(20)     NOT NULL,
    BarangayNo                 nvarchar(20)     NULL,
    Address                    nvarchar(255)    NULL,
    PasswordHash               nvarchar(255)    NULL,
    MustChangePassword         bit              NOT NULL CONSTRAINT DF_Parents_MustChangePassword DEFAULT (0),
    TemporaryPasswordExpiresAt datetime2        NULL,
    LastLogin                  datetime2        NULL,
    ConsentRecordedAt          datetime2        NULL,   -- Staff recorded Data Privacy consent
    PrivacyConsentAt           datetime2        NULL,   -- Parent accepted Data Privacy Notice
    CreatedAt                  datetime2        NOT NULL CONSTRAINT DF_Parents_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt                  datetime2        NOT NULL CONSTRAINT DF_Parents_UpdatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_Parents PRIMARY KEY (ParentID)
);

-- Login accounts (unified for both personnel and parents)
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    AccountID           uniqueidentifier NOT NULL CONSTRAINT DF_Accounts_AccountID DEFAULT (NEWID()),
    Username            nvarchar(100)    NOT NULL UNIQUE,
    PasswordHash        nvarchar(max)    NOT NULL,   -- BCrypt
    AccountType         nvarchar(20)     NOT NULL,   -- Personnel / Parent / SystemAdmin
    ReferenceID         uniqueidentifier NOT NULL,   -- Users.UserID or Parents.ParentID
    Status              bit              NOT NULL CONSTRAINT DF_Accounts_Status DEFAULT (1),
    MustChangePassword  bit              NOT NULL CONSTRAINT DF_Accounts_MustChangePassword DEFAULT (1),
    FailedLoginAttempts int              NOT NULL CONSTRAINT DF_Accounts_FailedLoginAttempts DEFAULT (0),
    LockedUntil         datetime2        NULL,
    LastLogin           datetime2        NULL,
    CreatedAt           datetime2        NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt           datetime2        NULL,
    CONSTRAINT PK_Accounts PRIMARY KEY (AccountID),
    CONSTRAINT CK_Accounts_AccountType CHECK (AccountType IN ('SystemAdmin', 'Personnel', 'Parent'))
);

-- One-time passwords (password reset OTPs)
IF OBJECT_ID(N'dbo.AccountOTPs', N'U') IS NULL
CREATE TABLE dbo.AccountOTPs (
    OTPID     uniqueidentifier NOT NULL CONSTRAINT DF_AccountOTPs_OTPID DEFAULT (NEWID()),
    AccountID uniqueidentifier NOT NULL,
    OTPHash   nvarchar(255)    NOT NULL,   -- BCrypt hash
    Purpose   nvarchar(50)     NOT NULL,   -- PasswordReset
    ExpiresAt datetime2        NOT NULL,
    IsUsed    bit              NOT NULL CONSTRAINT DF_AccountOTPs_IsUsed DEFAULT (0),
    Attempts  int              NOT NULL CONSTRAINT DF_AccountOTPs_Attempts DEFAULT (0),
    CreatedAt datetime2        NOT NULL CONSTRAINT DF_AccountOTPs_CreatedAt DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_AccountOTPs PRIMARY KEY (OTPID),
    CONSTRAINT FK_AccountOTPs_Accounts FOREIGN KEY (AccountID) REFERENCES dbo.Accounts (AccountID) ON DELETE CASCADE
);

/* =====================================================================
   CHILDREN AND FAMILY
   ===================================================================== */

-- Child (patient) records
IF OBJECT_ID(N'dbo.Children', N'U') IS NULL
CREATE TABLE dbo.Children (
    ChildID              uniqueidentifier NOT NULL CONSTRAINT DF_Children_ChildID DEFAULT (NEWID()),
    FirstName            nvarchar(50)     NOT NULL,
    MiddleName           nvarchar(50)     NULL,
    LastName             nvarchar(50)     NOT NULL,
    BirthDate            date             NOT NULL,
    PlaceOfBirth         nvarchar(100)    NULL,
    Address              nvarchar(70)     NULL,
    HealthCenter         nvarchar(100)    NULL,
    Barangay             int              NULL,
    Sex                  nvarchar(10)     NULL,
    Allergies            nvarchar(500)    NULL,
    CreatedAt            datetime2        NOT NULL CONSTRAINT DF_Children_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt            datetime2        NOT NULL CONSTRAINT DF_Children_UpdatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_Children PRIMARY KEY (ChildID)
);

-- Relationships between children and parents/guardians
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

CREATE NONCLUSTERED INDEX IX_ChildParentRelationship_ParentID ON dbo.ChildParentRelationship (ParentID);
CREATE NONCLUSTERED INDEX IX_ChildParentRelationship_ChildID ON dbo.ChildParentRelationship (ChildID);

/* =====================================================================
   VACCINES AND SCHEDULING
   ===================================================================== */

-- Vaccine master data (DOH EPI vaccines)
IF OBJECT_ID(N'dbo.Vaccines', N'U') IS NULL
CREATE TABLE dbo.Vaccines (
    VaccineID                int            IDENTITY(1,1) NOT NULL,
    VaccineName              nvarchar(100)  NOT NULL,
    Description              nvarchar(max)  NULL,
    Abbreviation             nvarchar(20)   NULL,
    TargetDisease            nvarchar(200)  NULL,
    RecommendedAge           nvarchar(100)  NULL,
    AgeCategory              nvarchar(50)   NULL,
    NumberOfRequiredDoses    int            NOT NULL CONSTRAINT DF_Vaccines_NumberOfRequiredDoses DEFAULT (1),
    DoseInterval             nvarchar(100)  NULL,
    AdministrationRoute      nvarchar(100)  NULL,
    Status                   bit            NOT NULL CONSTRAINT DF_Vaccines_Status DEFAULT (1),
    CreatedAt                datetime2      NOT NULL CONSTRAINT DF_Vaccines_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt                datetime2      NULL,
    CONSTRAINT PK_Vaccines PRIMARY KEY (VaccineID)
);

-- Vaccine dose intervals
IF OBJECT_ID(N'dbo.VaccineDoses', N'U') IS NULL
CREATE TABLE dbo.VaccineDoses (
    DoseID          int IDENTITY(1,1) NOT NULL,
    VaccineID       int NULL,
    DoseNumber      int NOT NULL,
    MinIntervalDays int NULL,
    CONSTRAINT PK_VaccineDoses PRIMARY KEY (DoseID),
    CONSTRAINT FK_VaccineDoses_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID)
);

-- Vaccination schedule rules (when each dose should be given)
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

-- Vaccine inventory (stock tracking by lot number)
IF OBJECT_ID(N'dbo.VaccineInventory', N'U') IS NULL
CREATE TABLE dbo.VaccineInventory (
    InventoryID       int           IDENTITY(1,1) NOT NULL,
    VaccineID         int           NOT NULL,
    LotNumber         nvarchar(100) NOT NULL UNIQUE,
    InitialQuantity   int           NOT NULL,
    CurrentQuantity   int           NOT NULL,
    MinimumStock      int           NOT NULL CONSTRAINT DF_VaccineInventory_MinimumStock DEFAULT (20),
    ExpirationDate    date          NOT NULL,
    ReceivedDate      date          NOT NULL,
    ManufacturingDate date          NULL,
    Supplier          nvarchar(255) NULL,
    Status            bit           NOT NULL CONSTRAINT DF_VaccineInventory_Status DEFAULT (1),
    CreatedAt         datetime      NOT NULL CONSTRAINT DF_VaccineInventory_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt         datetime      NULL,
    CONSTRAINT PK_VaccineInventory PRIMARY KEY (InventoryID),
    CONSTRAINT FK_VaccineInventory_Vaccine FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID)
);

CREATE NONCLUSTERED INDEX IX_VaccineInventory_VaccineID ON dbo.VaccineInventory (VaccineID);

/* =====================================================================
   VACCINATION RECORDS
   ===================================================================== */

-- Vaccination timeline for each child (expected and actual schedule)
IF OBJECT_ID(N'dbo.VaccinationTimeline', N'U') IS NULL
CREATE TABLE dbo.VaccinationTimeline (
    TimelineID          uniqueidentifier NOT NULL CONSTRAINT DF_VaccinationTimeline_TimelineID DEFAULT (NEWID()),
    TimelineCode        nvarchar(25)     NOT NULL UNIQUE,
    ChildID             uniqueidentifier NOT NULL,
    VaccineID           int              NOT NULL,
    DoseNumber          int              NOT NULL,
    ExpectedDate        date             NOT NULL,   -- birth date + recommended age
    ScheduledDate       date             NOT NULL,   -- moved to clinic operating day
    CompletedDate       date             NULL,
    VaccinationRecordID uniqueidentifier NULL,
    Status              nvarchar(20)     NOT NULL CONSTRAINT DF_VaccinationTimeline_Status DEFAULT ('Pending'),
    CreatedAt           datetime2        NOT NULL CONSTRAINT DF_VaccinationTimeline_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt           datetime2        NULL,
    CONSTRAINT PK_VaccineTimeline PRIMARY KEY (TimelineID),
    CONSTRAINT UQ_VaccineTimeline_ChildDose UNIQUE (ChildID, VaccineID, DoseNumber),
    CONSTRAINT FK_VaccineTimeline_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT FK_VaccineTimeline_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID),
    CONSTRAINT CK_VaccineTimeline_Status CHECK (Status IN ('Pending', 'Due', 'Completed', 'Overdue', 'Missed', 'Cancelled'))
);

CREATE NONCLUSTERED INDEX IX_VaccinationTimeline_ScheduledDate ON dbo.VaccinationTimeline (ScheduledDate, Status);

-- Actual vaccination records (doses administered)
IF OBJECT_ID(N'dbo.VaccinationRecords', N'U') IS NULL
CREATE TABLE dbo.VaccinationRecords (
    VaccinationRecordID  uniqueidentifier NOT NULL CONSTRAINT DF_VaccinationRecords_VaccinationRecordID DEFAULT (NEWID()),
    RecordCode           nvarchar(25)     NOT NULL UNIQUE,
    ChildID              uniqueidentifier NOT NULL,
    VaccineID            int              NOT NULL,
    InventoryID          int              NULL,    -- which lot/batch was used
    TimelineID           uniqueidentifier NULL,
    DoseNumber           int              NOT NULL,
    VaccinationDate      datetime2        NOT NULL,
    NurseObservation     nvarchar(max)    NULL,    -- reactions, remarks
    InjectionSite        nvarchar(30)     NULL,    -- Left thigh / Right upper arm / Mouth (oral)...
    Status               nvarchar(20)     NOT NULL CONSTRAINT DF_VaccinationRecords_Status DEFAULT ('Completed'),
    CreatedAt            datetime2        NOT NULL CONSTRAINT DF_VaccinationRecords_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt            datetime2        NULL,
    CONSTRAINT PK_VaccinationRecords PRIMARY KEY (VaccinationRecordID),
    CONSTRAINT UQ_VaccinationRecord_ChildDose UNIQUE (ChildID, VaccineID, DoseNumber),
    CONSTRAINT FK_VaccinationRecords_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT FK_VaccinationRecords_Vaccines FOREIGN KEY (VaccineID) REFERENCES dbo.Vaccines (VaccineID),
    CONSTRAINT FK_VaccinationRecords_Inventory FOREIGN KEY (InventoryID) REFERENCES dbo.VaccineInventory (InventoryID),
    CONSTRAINT FK_VaccinationRecords_Timeline FOREIGN KEY (TimelineID) REFERENCES dbo.VaccinationTimeline (TimelineID)
);

CREATE NONCLUSTERED INDEX IX_VaccinationRecords_VaccinationDate ON dbo.VaccinationRecords (VaccinationDate);

/* =====================================================================
   CLINIC OPERATIONS
   ===================================================================== */

-- Operating hours (Monday-Sunday, 8 AM - 12 PM typical)
IF OBJECT_ID(N'dbo.ClinicOperatingSchedule', N'U') IS NULL
CREATE TABLE dbo.ClinicOperatingSchedule (
    ScheduleID      int       IDENTITY(1,1) NOT NULL,
    DayOfWeek       int       NOT NULL,   -- 0 = Sunday ... 6 = Saturday
    IsOpen          bit       NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_IsOpen DEFAULT (0),
    OpeningTime     time      NOT NULL,
    ClosingTime     time      NOT NULL,
    QueueCutoffTime time      NOT NULL,   -- last time to check in
    IsActive        bit       NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_IsActive DEFAULT (1),
    CreatedAt       datetime2 NOT NULL CONSTRAINT DF_ClinicOperatingSchedule_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt       datetime2 NULL,
    CONSTRAINT PK_ClinicOperatingSchedule PRIMARY KEY (ScheduleID),
    CONSTRAINT CK_ClinicOperatingSchedule_DayOfWeek CHECK (DayOfWeek BETWEEN 0 AND 6),
    CONSTRAINT CK_ClinicOperatingSchedule_Time CHECK (OpeningTime < ClosingTime AND QueueCutoffTime >= OpeningTime AND QueueCutoffTime <= ClosingTime)
);

-- Exceptions to regular schedule (holidays, special openings)
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

CREATE UNIQUE NONCLUSTERED INDEX IX_ClinicScheduleExceptions_ExceptionDate ON dbo.ClinicScheduleExceptions (ExceptionDate) WHERE IsActive = 1;

-- Vaccination rooms/stations
IF OBJECT_ID(N'dbo.ClinicRooms', N'U') IS NULL
CREATE TABLE dbo.ClinicRooms (
    RoomID           int              IDENTITY(1,1) NOT NULL,
    RoomNumber       nvarchar(50)     NOT NULL,
    AssignedDoctorID uniqueidentifier NULL,
    IsOccupied       bit              NOT NULL CONSTRAINT DF_ClinicRooms_IsOccupied DEFAULT (0),
    CurrentChildID   uniqueidentifier NULL,
    CONSTRAINT PK_ClinicRooms PRIMARY KEY (RoomID),
    CONSTRAINT FK_ClinicRooms_AssignedDoctorID FOREIGN KEY (AssignedDoctorID) REFERENCES dbo.Users (UserID),
    CONSTRAINT FK_ClinicRooms_CurrentChildID FOREIGN KEY (CurrentChildID) REFERENCES dbo.Children (ChildID)
);

/* =====================================================================
   QUEUE MANAGEMENT
   ===================================================================== */

-- Daily queues (family visits)
IF OBJECT_ID(N'dbo.Queues', N'U') IS NULL
CREATE TABLE dbo.Queues (
    QueueID         uniqueidentifier NOT NULL,
    ParentID        uniqueidentifier NOT NULL,
    QueueNumber     int              NOT NULL,
    QueueDate       date             NOT NULL,
    Status          nvarchar(20)     NOT NULL CONSTRAINT DF_Queues_Status DEFAULT ('Waiting'),   -- Waiting / InProgress / Completed
    AssignedRoomID  int              NULL,
    AssignedStaffID uniqueidentifier NULL,
    CreatedAt       datetime         NOT NULL CONSTRAINT DF_Queues_CreatedAt DEFAULT (GETDATE()),
    UpdatedAt       datetime         NULL,
    CONSTRAINT PK_Queues PRIMARY KEY (QueueID),
    CONSTRAINT FK_Queues_Parents FOREIGN KEY (ParentID) REFERENCES dbo.Parents (ParentID),
    CONSTRAINT FK_Queues_ClinicRooms FOREIGN KEY (AssignedRoomID) REFERENCES dbo.ClinicRooms (RoomID)
);

CREATE NONCLUSTERED INDEX IX_Queues_QueueDate ON dbo.Queues (QueueDate, Status);
CREATE UNIQUE NONCLUSTERED INDEX IX_Queues_QueueDate_QueueNumber ON dbo.Queues (QueueDate, QueueNumber);

-- Children in each queue (one family may bring multiple children)
IF OBJECT_ID(N'dbo.QueueChildren', N'U') IS NULL
CREATE TABLE dbo.QueueChildren (
    QueueChildID uniqueidentifier NOT NULL,
    QueueID      uniqueidentifier NOT NULL,
    ChildID      uniqueidentifier NOT NULL,
    CreatedAt    datetime         NOT NULL CONSTRAINT DF_QueueChildren_CreatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_QueueChildren PRIMARY KEY (QueueChildID),
    CONSTRAINT FK_QueueChildren_Queues FOREIGN KEY (QueueID) REFERENCES dbo.Queues (QueueID) ON DELETE CASCADE,
    CONSTRAINT FK_QueueChildren_Children FOREIGN KEY (ChildID) REFERENCES dbo.Children (ChildID),
    CONSTRAINT UQ_QueueChildren_Queue_Child UNIQUE (QueueID, ChildID)
);

CREATE NONCLUSTERED INDEX IX_QueueChildren_QueueID ON dbo.QueueChildren (QueueID);
CREATE NONCLUSTERED INDEX IX_QueueChildren_ChildID ON dbo.QueueChildren (ChildID);

-- QR code settings
IF OBJECT_ID(N'dbo.QueueQRSettings', N'U') IS NULL
CREATE TABLE dbo.QueueQRSettings (
    SettingID int       IDENTITY(1,1) NOT NULL,
    IsEnabled bit       NOT NULL CONSTRAINT DF_QueueQRSettings_IsEnabled DEFAULT (1),
    UpdatedAt datetime2 NOT NULL CONSTRAINT DF_QueueQRSettings_UpdatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_QueueQRSettings PRIMARY KEY (SettingID)
);

-- Daily QR codes for check-in
IF OBJECT_ID(N'dbo.QueueQRCodes', N'U') IS NULL
CREATE TABLE dbo.QueueQRCodes (
    QRCodeID   uniqueidentifier NOT NULL CONSTRAINT DF_QueueQRCodes_QRCodeID DEFAULT (NEWID()),
    Token      nvarchar(200)    NOT NULL UNIQUE,   -- inside the QR
    ShortCode  nvarchar(10)     NOT NULL,   -- printed under the QR
    QRDate     date             NOT NULL,
    ValidFrom  datetime2        NOT NULL,
    ValidUntil datetime2        NOT NULL,
    IsActive   bit              NOT NULL CONSTRAINT DF_QueueQRCodes_IsActive DEFAULT (1),
    CreatedAt  datetime2        NOT NULL CONSTRAINT DF_QueueQRCodes_CreatedAt DEFAULT (SYSDATETIME()),
    CONSTRAINT PK_QueueQRCodes PRIMARY KEY (QRCodeID)
);

CREATE NONCLUSTERED INDEX IX_QueueQRCodes_QRDate ON dbo.QueueQRCodes (QRDate);

/* =====================================================================
   NOTIFICATIONS AND AUDIT
   ===================================================================== */

-- Notifications (to parents and staff)
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
CREATE TABLE dbo.Notifications (
    NotificationID   uniqueidentifier NOT NULL CONSTRAINT DF_Notifications_NotificationID DEFAULT (NEWID()),
    ParentID         uniqueidentifier NULL,
    ChildID          uniqueidentifier NULL,
    UserID           uniqueidentifier NULL,
    VaccineID        int              NULL,
    DoseNumber       int              NULL,
    Type             nvarchar(50)     NOT NULL,
    Title            nvarchar(200)    NOT NULL,
    Message          nvarchar(500)    NOT NULL,
    ScheduledDate    date             NULL,
    IsRead           bit              NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt        datetime         NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (GETDATE()),
    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationID)
);

CREATE NONCLUSTERED INDEX IX_Notifications_ParentID ON dbo.Notifications (ParentID);
CREATE NONCLUSTERED INDEX IX_Notifications_UserID ON dbo.Notifications (UserID);

-- Audit log of all system actions
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
CREATE TABLE dbo.AuditLogs (
    AuditID         bigint           IDENTITY(1,1) NOT NULL,
    UserID          uniqueidentifier NULL,
    ActionPerformed nvarchar(max)    NOT NULL,
    ActionDate      datetime         NULL CONSTRAINT DF_AuditLogs_ActionDate DEFAULT (GETDATE()),
    CONSTRAINT PK_AuditLogs PRIMARY KEY (AuditID),
    CONSTRAINT FK_AuditLogs_UserID FOREIGN KEY (UserID) REFERENCES dbo.Users (UserID)
);

CREATE NONCLUSTERED INDEX IX_AuditLogs_ActionDate ON dbo.AuditLogs (ActionDate);

GO

PRINT 'ArugaSystemDB schema created successfully.';
PRINT 'All tables, indexes, and relationships have been set up.';
PRINT 'No data has been inserted — use DemoSeed.sql for demo data.';
GO
