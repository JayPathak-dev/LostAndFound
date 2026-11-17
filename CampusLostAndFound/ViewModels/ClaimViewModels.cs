using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CampusLostAndFound.ViewModels
{
    public class ClaimCreateViewModel
    {
        [Required]
        public int ItemId { get; set; }

        public string ItemTitle { get; set; } = string.Empty;
        public string ItemCategory { get; set; } = string.Empty;
        public string ItemLocation { get; set; } = string.Empty;
        public DateTime FoundDateTime { get; set; }
        public string? ItemImagePath { get; set; }

        [Required(ErrorMessage = "Please describe why you believe this item belongs to you.")]
        [StringLength(2000, MinimumLength = 15, ErrorMessage = "Claim description must be at least 15 characters.")]
        [Display(Name = "Ownership Claim Statement")]
        public string ClaimDescription { get; set; } = string.Empty;

        // Dynamic verification challenges presented to user if questions exist
        public List<ChallengeAnswerInputModel> ChallengeAnswers { get; set; } = new();
    }

    public class ChallengeAnswerInputModel
    {
        public int ChallengeId { get; set; }
        public string QuestionPrompt { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide an answer to this verification question.")]
        [StringLength(1000)]
        [Display(Name = "Your Answer")]
        public string Answer { get; set; } = string.Empty;
    }

    public class ClaimDetailsViewModel
    {
        public int ClaimId { get; set; }
        public int ItemId { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string ItemCategory { get; set; } = string.Empty;
        public string ItemLocation { get; set; } = string.Empty;
        public string? ItemImagePath { get; set; }
        public string ItemStatus { get; set; } = string.Empty;

        public string ClaimantId { get; set; } = string.Empty;
        public string ClaimantName { get; set; } = string.Empty;
        public string ClaimantEmail { get; set; } = string.Empty;
        public string? ClaimantRegNo { get; set; }
        public string? ClaimantPhone { get; set; }

        public string ClaimDescription { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }

        public string? ReviewerName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }

        public List<VerificationChallengeResultViewModel> Challenges { get; set; } = new();

        public bool CanReview { get; set; }
        public bool CanInitiateHandover { get; set; }
        public bool HandoverCompleted { get; set; }
        public HandoverSummaryViewModel? HandoverDetails { get; set; }
    }

    public class ClaimReviewViewModel
    {
        [Required]
        public int ClaimId { get; set; }

        [Required(ErrorMessage = "Please select a decision.")]
        [Display(Name = "Decision Status")]
        public string Status { get; set; } = "Approved"; // Approved, Rejected, ManualReview

        [StringLength(1500)]
        [Display(Name = "Reviewer Notes & Justification")]
        public string? ReviewNotes { get; set; }
    }

    public class VerificationChallengeResultViewModel
    {
        public int ChallengeId { get; set; }
        public string QuestionPrompt { get; set; } = string.Empty;
        public string ChallengeType { get; set; } = string.Empty;
        public string? SubmittedAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public double? SimilarityScore { get; set; }

        // Only shown to authorized reviewers
        public string? ExpectedReference { get; set; }
    }

    public class HandoverSummaryViewModel
    {
        public int HandoverId { get; set; }
        public string SecurityOfficerName { get; set; } = string.Empty;
        public DateTime HandoverDateTime { get; set; }
        public string ProofReference { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}

