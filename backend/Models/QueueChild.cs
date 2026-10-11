using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    [Table("QueueChildren")]
    public class QueueChild
    {
        [Key]
        public Guid QueueChildID { get; set; }

        public Guid QueueID { get; set; }

        public Guid ChildID { get; set; }

        // Who actually brought this child when it isn't the parent, e.g. an
        // older sibling sent by the parent: typed in by staff at check-in
        // instead of registering them as a guardian. Null = the parent.
        // Per child, since a sibling may bring one child and an aunt
        // another later the same day, into the same family visit.
        public string? BroughtByName { get; set; }
        public string? BroughtByRelationship { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(QueueID))]
        public virtual Queue? Queue { get; set; }

        [ForeignKey(nameof(ChildID))]
        public virtual Child? Child { get; set; }
    }
}