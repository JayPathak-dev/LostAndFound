using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public class DisposalRecord
    {
        [Key]
        public int DisposalId { get; set; }

        [Required]
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        [Required]
        [StringLength(500)]
        public string DisposalReason { get; set; } = string.Empty;

        [Required]
        public string AuthorizedBy { get; set; } = string.Empty;
        [ForeignKey("AuthorizedBy")]
        public virtual ApplicationUser? AuthorizedByUser { get; set; }

        [Required]
        public DateTime DisposalDate { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}

