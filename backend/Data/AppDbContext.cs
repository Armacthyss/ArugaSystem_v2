using Microsoft.EntityFrameworkCore;
using AndroidWebAPI.Models;

namespace AndroidWebAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Child> Children { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<Queue> Queues { get; set; }
public DbSet<QueueChild> QueueChildren { get; set; }
        public DbSet<QueueQRSetting> QueueQRSettings { get; set; }
        public DbSet<QueueQRCode> QueueQRCodes { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountOtp> AccountOtps { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VaccinationRecord> VaccinationRecords { get; set; }
        public DbSet<VaccinationTimeline> VaccinationTimelines { get; set; }

        public DbSet<VaccinationScheduleRule> VaccinationScheduleRules { get; set; }
        public DbSet<Vaccine> Vaccines { get; set; }
        public DbSet<VaccineDose> VaccineDoses { get; set; }
        public DbSet<VaccineInventory> VaccineInventory { get; set; }
        
        public DbSet<ChildParentRelationship> ChildParentRelationships { get; set; }
     public DbSet<ClinicOperatingSchedule> ClinicOperatingSchedules { get; set; }
     public DbSet<ClinicRoom> ClinicRooms { get; set; }

     
public DbSet<ClinicScheduleException> ClinicScheduleExceptions { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
modelBuilder.Entity<Queue>()
    .HasKey(q => q.QueueID);

modelBuilder.Entity<Queue>()
    .HasOne(q => q.Parent)
    .WithMany()
    .HasForeignKey(q => q.ParentID)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<QueueChild>()
    .HasKey(qc => qc.QueueChildID);

modelBuilder.Entity<QueueChild>()
    .HasOne(qc => qc.Queue)
    .WithMany(q => q.QueueChildren)
    .HasForeignKey(qc => qc.QueueID)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<QueueChild>()
    .HasOne(qc => qc.Child)
    .WithMany()
    .HasForeignKey(qc => qc.ChildID)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<QueueQRSetting>()
    .HasKey(s => s.SettingID);

modelBuilder.Entity<QueueQRCode>()
    .HasKey(qr => qr.QRCodeID);
    
    modelBuilder.Entity<QueueQRCode>()
    .Property(qr => qr.QRDate)
    .HasColumnType("date");

            modelBuilder.Entity<ChildParentRelationship>(entity =>
            {
                entity.ToTable("ChildParentRelationship");

                entity.HasKey(e => e.RelationshipID);

                entity.HasOne(e => e.Child)
                      .WithMany(c => c.ParentRelationships)
                      .HasForeignKey(e => e.ChildID);

                entity.HasOne(e => e.Parent)
                      .WithMany(p => p.ChildRelationships)
                      .HasForeignKey(e => e.ParentID);
            });

            modelBuilder.Entity<VaccinationRecord>()
                .HasKey(v => v.VaccinationRecordID);
            modelBuilder.Entity<VaccinationTimeline>()
                .HasKey(v => v.TimelineID);

            modelBuilder.Entity<VaccinationScheduleRule>()
                .HasKey(v => v.RuleID);


            modelBuilder.Entity<Vaccine>()
                .HasKey(v => v.VaccineID);

            modelBuilder.Entity<VaccineDose>()
                .HasKey(v => v.DoseID);
modelBuilder.Entity<VaccinationRecord>()
    .HasOne(v => v.AdministeredBy)
    .WithMany()
    .HasForeignKey(v => v.AdministeredByUserID)
    .OnDelete(DeleteBehavior.Restrict);
            base.OnModelCreating(modelBuilder);
        }
    }
}