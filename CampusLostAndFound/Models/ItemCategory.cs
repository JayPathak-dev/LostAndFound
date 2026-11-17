using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CampusLostAndFound.Models
{
    public class ItemCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Configurable retention period in days before item is flagged for unclaimed disposal review.
        /// </summary>
        [Range(1, 365)]
        public int RetentionPeriodDays { get; set; } = 60;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();
    }
}

