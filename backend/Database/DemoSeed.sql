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
