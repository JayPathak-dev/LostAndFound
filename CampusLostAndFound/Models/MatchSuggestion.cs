using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public static class MatchReviewStatuses
    {
        public const string New = "New";
        public const string Viewed = "Viewed";
        public const string ClaimSubmitted = "ClaimSubmitted";
        public const string Dismissed = "Dismissed";
    }

    public class MatchSuggestion
    {
        [Key]
        public int MatchId { get; set; }

        [Required]
        public int LostItemId { get; set; }
        [ForeignKey("LostItemId")]
        public virtual Item? LostItem { get; set; }

        [Required]
        public int FoundItemId { get; set; }
        [ForeignKey("FoundItemId")]
        public virtual Item? FoundItem { get; set; }

        /// <summary>
        /// Match score from 0.0 to 100.0 computed by weighted matching engine.
        /// </summary>
        public double MatchScore { get; set; }

        [Required]
        [StringLength(1000)]
        public string MatchReason { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string ReviewStatus { get; set; } = MatchReviewStatuses.New;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
