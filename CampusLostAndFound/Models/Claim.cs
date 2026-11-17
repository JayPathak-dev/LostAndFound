using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public static class ClaimStatuses
    {
        public const string Pending = "Pending";
        public const string UnderVerification = "UnderVerification";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string ManualReview = "ManualReview";

        public static readonly string[] All = {
            Pending, UnderVerification, Approved, Rejected, ManualReview
        };
    }

    public class Claim
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        [Required]
        public string ClaimantId { get; set; } = string.Empty;
        [ForeignKey("ClaimantId")]
        public virtual ApplicationUser? Claimant { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string ClaimDescription { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = ClaimStatuses.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedBy { get; set; }
        [ForeignKey("ReviewedBy")]
        public virtual ApplicationUser? Reviewer { get; set; }

        [StringLength(1500)]
        public string? ReviewNotes { get; set; }

        // Navigation
        public virtual ICollection<VerificationChallenge> Challenges { get; set; } = new List<VerificationChallenge>();
        public virtual Handover? Handover { get; set; }
    }
}

