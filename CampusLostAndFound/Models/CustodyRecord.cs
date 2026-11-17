using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public class CustodyRecord
    {
        [Key]
        public int CustodyId { get; set; }

        [Required]
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        [Required]
        [StringLength(200)]
        public string CustodyLocation { get; set; } = string.Empty; // e.g. "Main Security Desk, Locker B-12"

        [Required]
        public string ResponsibleSecurityUserId { get; set; } = string.Empty;
        [ForeignKey("ResponsibleSecurityUserId")]
        public virtual ApplicationUser? ResponsibleSecurityUser { get; set; }

        [Required]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReleasedAt { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}

