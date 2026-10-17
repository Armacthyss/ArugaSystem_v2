using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    // One change to a batch's dose count that isn't a vaccination (Oct 2026):
    // broken or spoiled doses, expired doses thrown away, lost doses, or a
    // recount that found a different number on the shelf. Kept so the Doctor
    // can see why the count changed, and so the inventory summary adds up.
    public class StockAdjustment
    {
        [Key]
        public int AdjustmentID { get; set; }

        public int InventoryID { get; set; }

        // Doses added (+) or taken off (-) by this adjustment
        public int Change { get; set; }

        public int QuantityBefore { get; set; }

        public int QuantityAfter { get; set; }

        // "Broken vial", "Spoiled (cold chain)", "Expired, thrown away", "Lost", "Recount", "Other"
        public string Reason { get; set; } = string.Empty;

        public string? Note { get; set; }

        // Users.UserID of the Doctor / Nurse, and their name as it was then
        public Guid? AdjustedByUserID { get; set; }

        public string? AdjustedByName { get; set; }

        public DateTime AdjustedAt { get; set; }

        [ForeignKey(nameof(InventoryID))]
        public VaccineInventory? Inventory { get; set; }
    }
}
