using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public class Handover
    {
        [Key]
        public int HandoverId { get; set; }

        [Required]
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        [Required]
        public int ClaimId { get; set; }
        [ForeignKey("ClaimId")]
        public virtual Claim? Claim { get; set; }

        [Required]
        public string ReceivingUserId { get; set; } = string.Empty;
        [ForeignKey("ReceivingUserId")]
        public virtual ApplicationUser? ReceivingUser { get; set; }

        [Required]
        public string SecurityUserId { get; set; } = string.Empty;
        [ForeignKey("SecurityUserId")]
        public virtual ApplicationUser? SecurityUser { get; set; }

        [Required]
        public DateTime HandoverDateTime { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(200)]
        public string ProofReference { get; set; } = string.Empty; // e.g. Student ID #verified / Govt ID last 4 digits / Verification Slip

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}
