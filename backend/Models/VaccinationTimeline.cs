using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    [Table("VaccinationTimeline")]
    public class VaccinationTimeline
    {
        [Key]
        public Guid TimelineID { get; set; }

        public string TimelineCode { get; set; } = string.Empty;

        public Guid ChildID { get; set; }

        public int VaccineID { get; set; }

        public int DoseNumber { get; set; }

        // Date computed from the vaccination rules
        public DateTime ExpectedDate { get; set; }

        // Date adjusted to clinic operating schedule
        public DateTime ScheduledDate { get; set; }

        // Actual administration date, set when the dose is recorded
        public DateTime? CompletedDate { get; set; }

        // Completed / Pending / Missed / Cancelled
        public string Status { get; set; } = "Pending";

        // Links to VaccinationRecord after vaccination
        public Guid? VaccinationRecordID { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        //------------------------------------
        // Navigation Properties
        //------------------------------------

        [ForeignKey(nameof(ChildID))]
        public virtual Child Child { get; set; }

        [ForeignKey(nameof(VaccineID))]
        public virtual Vaccine Vaccine { get; set; }

        [ForeignKey(nameof(VaccinationRecordID))]
        public virtual VaccinationRecord? VaccinationRecord { get; set; }
    }
}