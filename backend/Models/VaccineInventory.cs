using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    public class VaccineInventory
    {
        [Key]
        public int InventoryID { get; set; }

        public int VaccineID { get; set; }

        public string LotNumber { get; set; } = string.Empty;

        public int InitialQuantity { get; set; }

        public int CurrentQuantity { get; set; }

        public int MinimumStock { get; set; }

        public DateTime ExpirationDate { get; set; }

        public DateTime ReceivedDate { get; set; }

        public DateTime? ManufacturingDate { get; set; }

        public string? Supplier { get; set; }

        public bool Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(VaccineID))]
        public Vaccine? Vaccine { get; set; }
    }
}