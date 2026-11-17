using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public static class ReportTypes
    {
        public const string Lost = "Lost";
        public const string Found = "Found";
    }

    public static class ItemStatuses
    {
        public const string Reported = "Reported";
        public const string Active = "Active";
        public const string Claimed = "Claimed";
        public const string UnderVerification = "UnderVerification";
        public const string Approved = "Approved";
        public const string Returned = "Returned";
        public const string Unclaimed = "Unclaimed";
        public const string Disposed = "Disposed";

        public static readonly string[] All = {
            Reported, Active, Claimed, UnderVerification, Approved, Returned, Unclaimed, Disposed
        };
    }

    public class Item
    {
        [Key]
        public int ItemId { get; set; }

        [Required]
        [StringLength(150, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2500, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ReportType { get; set; } = ReportTypes.Found; // Lost or Found

        [Required]
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual ItemCategory? Category { get; set; }

        [Required]
        public int LocationId { get; set; }
        [ForeignKey("LocationId")]
        public virtual CampusLocation? Location { get; set; }

        [Required]
        public DateTime ItemDateTime { get; set; } = DateTime.UtcNow;

        [StringLength(300)]
        public string? ImagePath { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = ItemStatuses.Active;

        [Required]
        public string ReporterId { get; set; } = string.Empty;
        [ForeignKey("ReporterId")]
        public virtual ApplicationUser? Reporter { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual PrivateItemDetail? PrivateDetail { get; set; }
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
        public virtual ICollection<MatchSuggestion> MatchSuggestionsAsLost { get; set; } = new List<MatchSuggestion>();
        public virtual ICollection<MatchSuggestion> MatchSuggestionsAsFound { get; set; } = new List<MatchSuggestion>();
        public virtual ICollection<CustodyRecord> CustodyRecords { get; set; } = new List<CustodyRecord>();
        public virtual Handover? Handover { get; set; }
        public virtual DisposalRecord? DisposalRecord { get; set; }
    }
}

