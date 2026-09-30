using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AndroidWebAPI.Models
{
    // Maps to the existing dbo.AuditLogs table (AuditID is an IDENTITY column).
    // ActionPerformed holds a small JSON document written by AuditService
    // (module, action, record, status, ip, ...). Older rows that were
    // inserted by hand hold a plain sentence instead — AuditLogsController
    // reads both.
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long AuditID { get; set; }

        public Guid? UserID { get; set; }

        public string ActionPerformed { get; set; } = string.Empty;

        public DateTime? ActionDate { get; set; }
    }
}
