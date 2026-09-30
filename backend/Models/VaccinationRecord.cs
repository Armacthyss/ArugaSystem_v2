using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    [Table("VaccinationRecords")]
    public class VaccinationRecord
    {
        [Key]
        public Guid VaccinationRecordID { get; set; }

        public string? RecordCode { get; set; }

        public Guid ChildID { get; set; }

        public int VaccineID { get; set; }

        // Nullable because historical records may not have
        // a corresponding inventory item in our clinic.
        public int? InventoryID { get; set; }

        // Nullable because a historical vaccination may not
        // have been linked to a generated timeline yet.
        public Guid? TimelineID { get; set; }

        public int DoseNumber { get; set; }

        public DateTime VaccinationDate { get; set; }

        // Personnel who entered/administered the record.
        public Guid? AdministeredByUserID { get; set; }

        public string? NurseObservation { get; set; }

        // Where the dose was given ("Left thigh", "Right upper arm",
        // "Mouth (oral)"...), so a later swelling can be traced to the
        // vaccine (asked for by City Hall). Null for older/Yellow Book doses.
        public string? InjectionSite { get; set; }

        // Completed / Cancelled / Deferred
        public string Status { get; set; } = "Completed";

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // ------------------------------------
        // Navigation Properties
        // ------------------------------------

        [ForeignKey(nameof(ChildID))]
        public virtual Child? Child { get; set; }

        [ForeignKey(nameof(VaccineID))]
        public virtual Vaccine? Vaccine { get; set; }

        [ForeignKey(nameof(InventoryID))]
        public virtual VaccineInventory? Inventory { get; set; }

        [ForeignKey(nameof(TimelineID))]
        public virtual VaccinationTimeline? Timeline { get; set; }

        [ForeignKey(nameof(AdministeredByUserID))]
        public virtual User? AdministeredBy { get; set; }
    }
}