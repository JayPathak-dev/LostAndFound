using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CampusLostAndFound.Models;

namespace CampusLostAndFound.ViewModels
{
    public class UserDashboardViewModel
    {
        public int TotalLostReports { get; set; }
        public int TotalFoundReports { get; set; }
        public int ActiveClaimsCount { get; set; }
        public int ItemsRecoveredCount { get; set; }

        public List<ItemCardViewModel> MyRecentReports { get; set; } = new();
        public List<ClaimSummaryViewModel> MyRecentClaims { get; set; } = new();
        public List<MatchSuggestionCardViewModel> MatchSuggestions { get; set; } = new();
    }

    public class ClaimSummaryViewModel
    {
        public int ClaimId { get; set; }
        public int ItemId { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class MatchSuggestionCardViewModel
    {
        public int MatchId { get; set; }
        public int LostItemId { get; set; }
        public string LostItemTitle { get; set; } = string.Empty;
        public int FoundItemId { get; set; }
        public string FoundItemTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public string MatchReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string ReviewStatus { get; set; } = string.Empty;
    }

    public class SecurityDashboardViewModel
    {
        public int ItemsInCustodyCount { get; set; }
        public int PendingClaimsCount { get; set; }
        public int ApprovedAwaitingHandoverCount { get; set; }
        public int TotalReturnedCount { get; set; }
        public int UnclaimedCount { get; set; }

        public List<ClaimDetailsViewModel> PendingClaims { get; set; } = new();
        public List<ClaimDetailsViewModel> ApprovedClaimsForHandover { get; set; } = new();
        public List<CustodyItemViewModel> ItemsInCustody { get; set; } = new();
        public List<HandoverRecordViewModel> RecentHandovers { get; set; } = new();
    }

    public class CustodyItemViewModel
    {
        public int CustodyId { get; set; }
        public int ItemId { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CustodyLocation { get; set; } = string.Empty;
        public string SecurityOfficerName { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public string ItemStatus { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class HandoverRecordViewModel
    {
        public int HandoverId { get; set; }
        public int ItemId { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string ReceivingUserName { get; set; } = string.Empty;
        public string ReceivingUserRegNo { get; set; } = string.Empty;
        public string SecurityOfficerName { get; set; } = string.Empty;
        public DateTime HandoverDateTime { get; set; }
        public string ProofReference { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class HandoverCreateViewModel
    {
        [Required]
        public int ClaimId { get; set; }

        [Required]
        public int ItemId { get; set; }

        public string ItemTitle { get; set; } = string.Empty;
        public string ClaimantName { get; set; } = string.Empty;
        public string ClaimantEmail { get; set; } = string.Empty;
        public string? ClaimantRegNo { get; set; }
        public string? ClaimantPhone { get; set; }

        [Required(ErrorMessage = "Verification proof reference is required.")]
        [StringLength(200, ErrorMessage = "Proof reference cannot exceed 200 characters.")]
        [Display(Name = "Proof of Identity / Verification Slip")]
        public string ProofReference { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Supervised Handover Remarks")]
        public string? Notes { get; set; }
    }

    public class CustodyAssignViewModel
    {
        [Required]
        public int ItemId { get; set; }

        public string ItemTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Custody location is required.")]
        [StringLength(200)]
        [Display(Name = "Physical Storage Location (e.g., Locker Room A, Shelf 3)")]
        public string CustodyLocation { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Custody Notes")]
        public string? Notes { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalLost { get; set; }
        public int TotalFound { get; set; }
        public int TotalReturned { get; set; }
        public int PendingClaims { get; set; }
        public int ItemsInCustody { get; set; }
        public int UnclaimedCount { get; set; }
        public int DisposedCount { get; set; }
        public double RecoveryRatePercentage { get; set; }

        public Dictionary<string, int> CategoryBreakdown { get; set; } = new();
        public Dictionary<string, int> LocationBreakdown { get; set; } = new();
        public List<AuditEntry> RecentAuditLogs { get; set; } = new();
        public List<UnclaimedItemViewModel> UnclaimedItems { get; set; } = new();
    }

    public class UnclaimedItemViewModel
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public DateTime FoundDate { get; set; }
        public int DaysInCustody { get; set; }
        public int RetentionPeriodDays { get; set; }
        public bool IsOverdueForDisposal => DaysInCustody >= RetentionPeriodDays;
        public string CurrentStatus { get; set; } = string.Empty;
    }

    public class DisposalCreateViewModel
    {
        [Required]
        public int ItemId { get; set; }

        public string ItemTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int DaysInCustody { get; set; }

        [Required(ErrorMessage = "Disposal reason is required.")]
        [StringLength(500)]
        [Display(Name = "Authorized Reason for Disposal (e.g., Donated, Recycled)")]
        public string DisposalReason { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Additional Notes & Approval Details")]
        public string? Notes { get; set; }
    }

    public class CategoryFormViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Category Name is required.")]
        [StringLength(100)]
        [Display(Name = "Category Name")]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Retention period is required.")]
        [Range(1, 365, ErrorMessage = "Retention period must be between 1 and 365 days.")]
        [Display(Name = "Default Retention Period (Days)")]
        public int RetentionPeriodDays { get; set; } = 60;

        [Display(Name = "Active?")]
        public bool IsActive { get; set; } = true;
    }

    public class LocationFormViewModel
    {
        public int LocationId { get; set; }

        [Required(ErrorMessage = "Building Name is required.")]
        [StringLength(150)]
        [Display(Name = "Building Name")]
        public string BuildingName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Block / Wing (Optional)")]
        public string? BlockName { get; set; }

        [StringLength(50)]
        [Display(Name = "Floor (Optional)")]
        public string? Floor { get; set; }

        [Required(ErrorMessage = "Area / Room Name is required.")]
        [StringLength(150)]
        [Display(Name = "Specific Area / Room Name")]
        public string AreaName { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description & Landmark")]
        public string? Description { get; set; }

        [Display(Name = "Active?")]
        public bool IsActive { get; set; } = true;
    }
}

