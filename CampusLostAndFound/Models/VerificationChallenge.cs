using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public class VerificationChallenge
    {
        [Key]
        public int ChallengeId { get; set; }

        [Required]
        public int ClaimId { get; set; }
        [ForeignKey("ClaimId")]
        public virtual Claim? Claim { get; set; }

        [Required]
        [StringLength(500)]
        public string QuestionPrompt { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ChallengeType { get; set; } = string.Empty; // e.g. "InternalContents", "DistinctiveMarks", "HiddenFeatures"

        /// <summary>
        /// Salted SHA-256 hash or normalized signature of expected key verification elements.
        /// Expected answers are never sent to the client browser.
        /// </summary>
        [Required]
        [StringLength(500)]
        public string ExpectedAnswerHash { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? SubmittedAnswer { get; set; }

        public bool? IsCorrect { get; set; }

        public double? SimilarityScore { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AnsweredAt { get; set; }
    }
}

