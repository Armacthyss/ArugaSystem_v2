/* =====================================================================
   ARUGA — REMOVE DEMO DATA
   Deletes everything DemoSeed.sql created (IDs starting A2A1…A2A7 and
   vaccine batches with lot numbers starting DEMO-), plus anything that
   was recorded ON those demo people during testing.
   The rooms (Room 1, Room 2, …) are real and stay; the demo health
   workers are just taken off them.
   Real (non-demo) data is left untouched.
   Open in SSMS and press Execute: it switches to ArugaSystemDB by itself.
   ===================================================================== */

USE ArugaSystemDB;   -- if the database doesn't exist, nothing below runs

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @Child TABLE (ID uniqueidentifier PRIMARY KEY);
DECLARE @Parent TABLE (ID uniqueidentifier PRIMARY KEY);
DECLARE @User TABLE (ID uniqueidentifier PRIMARY KEY);
INSERT @Child  SELECT ChildID  FROM Children WHERE CONVERT(char(36), ChildID)  LIKE 'A2A30000-%';
INSERT @Parent SELECT ParentID FROM Parents  WHERE CONVERT(char(36), ParentID) LIKE 'A2A20000-%';
INSERT @User   SELECT UserID   FROM Users    WHERE CONVERT(char(36), UserID)   LIKE 'A2A10000-%';

UPDATE ClinicRooms SET CurrentChildID = NULL, IsOccupied = 0 WHERE CurrentChildID IN (SELECT ID FROM @Child);
UPDATE ClinicRooms SET AssignedDoctorID = NULL WHERE AssignedDoctorID IN (SELECT ID FROM @User);

DELETE FROM QueueChildren WHERE ChildID IN (SELECT ID FROM @Child)
    OR QueueID IN (SELECT QueueID FROM Queues WHERE ParentID IN (SELECT ID FROM @Parent));
DELETE FROM Queues WHERE ParentID IN (SELECT ID FROM @Parent);

-- Stations named Station A/B/C came from an older version of DemoSeed.sql.
-- Any real visit that was sent to one keeps its record, just without the station.
DECLARE @Room TABLE (ID int PRIMARY KEY);
INSERT @Room SELECT RoomID FROM ClinicRooms WHERE RoomNumber IN (N'Station A', N'Station B', N'Station C');
UPDATE Queues SET AssignedRoomID = NULL WHERE AssignedRoomID IN (SELECT ID FROM @Room);
DELETE FROM ClinicRooms WHERE RoomID IN (SELECT ID FROM @Room);

DELETE FROM Notifications WHERE ChildID IN (SELECT ID FROM @Child)
    OR ParentID IN (SELECT ID FROM @Parent) OR UserID IN (SELECT ID FROM @User);

UPDATE VaccinationTimeline SET VaccinationRecordID = NULL WHERE ChildID IN (SELECT ID FROM @Child);
DELETE FROM VaccinationRecords WHERE ChildID IN (SELECT ID FROM @Child);
DELETE FROM VaccinationTimeline WHERE ChildID IN (SELECT ID FROM @Child);

-- Doses given to real children BY a demo worker keep the record, just
-- without the demo worker attached.
UPDATE VaccinationRecords SET AdministeredByUserID = NULL WHERE AdministeredByUserID IN (SELECT ID FROM @User);

DELETE FROM ChildParentRelationship WHERE ChildID IN (SELECT ID FROM @Child) OR ParentID IN (SELECT ID FROM @Parent);
DELETE FROM Children WHERE ChildID IN (SELECT ID FROM @Child);

DELETE FROM AuditLogs WHERE UserID IN (SELECT ID FROM @User) OR ActionPerformed LIKE '%"Demo":true%';
DELETE FROM AccountOTPs WHERE AccountID IN (SELECT AccountID FROM Accounts WHERE CONVERT(char(36), AccountID) LIKE 'A2A40000-%');
DELETE FROM Accounts WHERE CONVERT(char(36), AccountID) LIKE 'A2A40000-%'
    OR ReferenceID IN (SELECT ID FROM @Parent) OR ReferenceID IN (SELECT ID FROM @User);
DELETE FROM Parents WHERE ParentID IN (SELECT ID FROM @Parent);
DELETE FROM Users WHERE UserID IN (SELECT ID FROM @User);

-- Demo batches, unless a real child's record still points at one
DELETE v FROM VaccineInventory v
WHERE v.LotNumber LIKE 'DEMO-%'
  AND NOT EXISTS (SELECT 1 FROM VaccinationRecords r WHERE r.InventoryID = v.InventoryID);

COMMIT TRANSACTION;

PRINT 'Aruga demo data removed.';
