using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndroidWebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueStaffAndVaccinationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountID);
                });

            migrationBuilder.CreateTable(
                name: "Children",
                columns: table => new
                {
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HealthCenter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Barangay = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Allergies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BirthHeight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BirthWeight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Children", x => x.ChildID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicOperatingSchedule",
                columns: table => new
                {
                    ScheduleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    OpeningTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ClosingTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    QueueCutoffTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicOperatingSchedule", x => x.ScheduleID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicRooms",
                columns: table => new
                {
                    RoomID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedDoctorID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsOccupied = table.Column<bool>(type: "bit", nullable: false),
                    CurrentChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicRooms", x => x.RoomID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicScheduleExceptions",
                columns: table => new
                {
                    ExceptionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExceptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    OpeningTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ClosingTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    QueueCutoffTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicScheduleExceptions", x => x.ExceptionID);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VaccineID = table.Column<int>(type: "int", nullable: true),
                    DoseNumber = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationID);
                });

            migrationBuilder.CreateTable(
                name: "Parents",
                columns: table => new
                {
                    ParentID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BarangayNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    TemporaryPasswordExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parents", x => x.ParentID);
                });

            migrationBuilder.CreateTable(
                name: "Personnel",
                columns: table => new
                {
                    PersonnelID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonnelCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personnel", x => x.PersonnelID);
                });

            migrationBuilder.CreateTable(
                name: "QueueQRCodes",
                columns: table => new
                {
                    QRCodeID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QRDate = table.Column<DateTime>(type: "date", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueQRCodes", x => x.QRCodeID);
                });

            migrationBuilder.CreateTable(
                name: "QueueQRSettings",
                columns: table => new
                {
                    SettingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueQRSettings", x => x.SettingID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRCNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "Vaccines",
                columns: table => new
                {
                    VaccineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccineName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgeCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetDisease = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendedAge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberOfRequiredDoses = table.Column<int>(type: "int", nullable: false),
                    DoseInterval = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdministrationRoute = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vaccines", x => x.VaccineID);
                });

            migrationBuilder.CreateTable(
                name: "ChildParentRelationship",
                columns: table => new
                {
                    RelationshipID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false),
                    CanReceiveNotifications = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildParentRelationship", x => x.RelationshipID);
                    table.ForeignKey(
                        name: "FK_ChildParentRelationship_Children_ChildID",
                        column: x => x.ChildID,
                        principalTable: "Children",
                        principalColumn: "ChildID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildParentRelationship_Parents_ParentID",
                        column: x => x.ParentID,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Queues",
                columns: table => new
                {
                    QueueID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueNumber = table.Column<int>(type: "int", nullable: false),
                    QueueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedRoomID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedStaffID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VaccinationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Queues", x => x.QueueID);
                    table.ForeignKey(
                        name: "FK_Queues_Parents_ParentID",
                        column: x => x.ParentID,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Queues_Personnel_AssignedStaffID",
                        column: x => x.AssignedStaffID,
                        principalTable: "Personnel",
                        principalColumn: "PersonnelID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccountOtps",
                columns: table => new
                {
                    OTPID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OTPCodeHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountOtps", x => x.OTPID);
                    table.ForeignKey(
                        name: "FK_AccountOtps_Parents_ParentID",
                        column: x => x.ParentID,
                        principalTable: "Parents",
                        principalColumn: "ParentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountOtps_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationScheduleRules",
                columns: table => new
                {
                    RuleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccineID = table.Column<int>(type: "int", nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    MinimumAgeDays = table.Column<int>(type: "int", nullable: false),
                    RecommendedAgeDays = table.Column<int>(type: "int", nullable: false),
                    IntervalFromPreviousDoseDays = table.Column<int>(type: "int", nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationScheduleRules", x => x.RuleID);
                    table.ForeignKey(
                        name: "FK_VaccinationScheduleRules_Vaccines_VaccineID",
                        column: x => x.VaccineID,
                        principalTable: "Vaccines",
                        principalColumn: "VaccineID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccineDoses",
                columns: table => new
                {
                    DoseID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccineID = table.Column<int>(type: "int", nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    MinIntervalDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccineDoses", x => x.DoseID);
                    table.ForeignKey(
                        name: "FK_VaccineDoses_Vaccines_VaccineID",
                        column: x => x.VaccineID,
                        principalTable: "Vaccines",
                        principalColumn: "VaccineID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccineInventory",
                columns: table => new
                {
                    InventoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccineID = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false),
                    CurrentQuantity = table.Column<int>(type: "int", nullable: false),
                    MinimumStock = table.Column<int>(type: "int", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Supplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccineInventory", x => x.InventoryID);
                    table.ForeignKey(
                        name: "FK_VaccineInventory_Vaccines_VaccineID",
                        column: x => x.VaccineID,
                        principalTable: "Vaccines",
                        principalColumn: "VaccineID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueueChildren",
                columns: table => new
                {
                    QueueChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueChildren", x => x.QueueChildID);
                    table.ForeignKey(
                        name: "FK_QueueChildren_Children_ChildID",
                        column: x => x.ChildID,
                        principalTable: "Children",
                        principalColumn: "ChildID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueChildren_Queues_QueueID",
                        column: x => x.QueueID,
                        principalTable: "Queues",
                        principalColumn: "QueueID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationRecords",
                columns: table => new
                {
                    VaccinationRecordID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaccineID = table.Column<int>(type: "int", nullable: false),
                    InventoryID = table.Column<int>(type: "int", nullable: true),
                    TimelineID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    VaccinationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdministeredByUserID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NurseObservation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorDiagnosis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorDiagnosedByUserID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DoctorDiagnosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PersonnelID = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationRecords", x => x.VaccinationRecordID);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Children_ChildID",
                        column: x => x.ChildID,
                        principalTable: "Children",
                        principalColumn: "ChildID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Personnel_PersonnelID",
                        column: x => x.PersonnelID,
                        principalTable: "Personnel",
                        principalColumn: "PersonnelID");
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Users_AdministeredByUserID",
                        column: x => x.AdministeredByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Users_DoctorDiagnosedByUserID",
                        column: x => x.DoctorDiagnosedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_VaccineInventory_InventoryID",
                        column: x => x.InventoryID,
                        principalTable: "VaccineInventory",
                        principalColumn: "InventoryID");
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Vaccines_VaccineID",
                        column: x => x.VaccineID,
                        principalTable: "Vaccines",
                        principalColumn: "VaccineID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationTimeline",
                columns: table => new
                {
                    TimelineID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimelineCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChildID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaccineID = table.Column<int>(type: "int", nullable: false),
                    DoseNumber = table.Column<int>(type: "int", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VaccinationRecordID = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationTimeline", x => x.TimelineID);
                    table.ForeignKey(
                        name: "FK_VaccinationTimeline_Children_ChildID",
                        column: x => x.ChildID,
                        principalTable: "Children",
                        principalColumn: "ChildID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaccinationTimeline_VaccinationRecords_VaccinationRecordID",
                        column: x => x.VaccinationRecordID,
                        principalTable: "VaccinationRecords",
                        principalColumn: "VaccinationRecordID");
                    table.ForeignKey(
                        name: "FK_VaccinationTimeline_Vaccines_VaccineID",
                        column: x => x.VaccineID,
                        principalTable: "Vaccines",
                        principalColumn: "VaccineID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountOtps_ParentID",
                table: "AccountOtps",
                column: "ParentID");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOtps_UserID",
                table: "AccountOtps",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ChildParentRelationship_ChildID",
                table: "ChildParentRelationship",
                column: "ChildID");

            migrationBuilder.CreateIndex(
                name: "IX_ChildParentRelationship_ParentID",
                table: "ChildParentRelationship",
                column: "ParentID");

            migrationBuilder.CreateIndex(
                name: "IX_QueueChildren_ChildID",
                table: "QueueChildren",
                column: "ChildID");

            migrationBuilder.CreateIndex(
                name: "IX_QueueChildren_QueueID",
                table: "QueueChildren",
                column: "QueueID");

            migrationBuilder.CreateIndex(
                name: "IX_Queues_AssignedStaffID",
                table: "Queues",
                column: "AssignedStaffID");

            migrationBuilder.CreateIndex(
                name: "IX_Queues_ParentID",
                table: "Queues",
                column: "ParentID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_AdministeredByUserID",
                table: "VaccinationRecords",
                column: "AdministeredByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_ChildID",
                table: "VaccinationRecords",
                column: "ChildID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_DoctorDiagnosedByUserID",
                table: "VaccinationRecords",
                column: "DoctorDiagnosedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_InventoryID",
                table: "VaccinationRecords",
                column: "InventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_PersonnelID",
                table: "VaccinationRecords",
                column: "PersonnelID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_TimelineID",
                table: "VaccinationRecords",
                column: "TimelineID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_VaccineID",
                table: "VaccinationRecords",
                column: "VaccineID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationScheduleRules_VaccineID",
                table: "VaccinationScheduleRules",
                column: "VaccineID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationTimeline_ChildID",
                table: "VaccinationTimeline",
                column: "ChildID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationTimeline_VaccinationRecordID",
                table: "VaccinationTimeline",
                column: "VaccinationRecordID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationTimeline_VaccineID",
                table: "VaccinationTimeline",
                column: "VaccineID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccineDoses_VaccineID",
                table: "VaccineDoses",
                column: "VaccineID");

            migrationBuilder.CreateIndex(
                name: "IX_VaccineInventory_VaccineID",
                table: "VaccineInventory",
                column: "VaccineID");

            migrationBuilder.AddForeignKey(
                name: "FK_VaccinationRecords_VaccinationTimeline_TimelineID",
                table: "VaccinationRecords",
                column: "TimelineID",
                principalTable: "VaccinationTimeline",
                principalColumn: "TimelineID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Users_AdministeredByUserID",
                table: "VaccinationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Users_DoctorDiagnosedByUserID",
                table: "VaccinationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Children_ChildID",
                table: "VaccinationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationTimeline_Children_ChildID",
                table: "VaccinationTimeline");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Personnel_PersonnelID",
                table: "VaccinationRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_VaccinationTimeline_TimelineID",
                table: "VaccinationRecords");

            migrationBuilder.DropTable(
                name: "AccountOtps");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "ChildParentRelationship");

            migrationBuilder.DropTable(
                name: "ClinicOperatingSchedule");

            migrationBuilder.DropTable(
                name: "ClinicRooms");

            migrationBuilder.DropTable(
                name: "ClinicScheduleExceptions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "QueueChildren");

            migrationBuilder.DropTable(
                name: "QueueQRCodes");

            migrationBuilder.DropTable(
                name: "QueueQRSettings");

            migrationBuilder.DropTable(
                name: "VaccinationScheduleRules");

            migrationBuilder.DropTable(
                name: "VaccineDoses");

            migrationBuilder.DropTable(
                name: "Queues");

            migrationBuilder.DropTable(
                name: "Parents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Children");

            migrationBuilder.DropTable(
                name: "Personnel");

            migrationBuilder.DropTable(
                name: "VaccinationTimeline");

            migrationBuilder.DropTable(
                name: "VaccinationRecords");

            migrationBuilder.DropTable(
                name: "VaccineInventory");

            migrationBuilder.DropTable(
                name: "Vaccines");
        }
    }
}
