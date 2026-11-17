using System;
using System.ComponentModel.DataAnnotations;

namespace CampusLostAndFound.Models
{
    public class AuditEntry
    {
        [Key]
        public int AuditId { get; set; }

        public string? ActorId { get; set; }

        [StringLength(256)]
        public string? ActorEmail { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TargetRecord { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TargetRecordId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? AdditionalInformation { get; set; }
    }
}

