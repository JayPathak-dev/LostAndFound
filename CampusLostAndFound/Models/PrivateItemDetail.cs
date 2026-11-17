using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusLostAndFound.Models
{
    public class PrivateItemDetail
    {
        [Key]
        public int PrivateDetailId { get; set; }

        [Required]
        public int ItemId { get; set; }
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }

        [StringLength(1000)]
        public string? DistinctiveMarks { get; set; }

        [StringLength(1000)]
        public string? HiddenFeatures { get; set; }

        [StringLength(1000)]
        public string? InternalContents { get; set; }

        [StringLength(1000)]
        public string? AdditionalIdentifiers { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
