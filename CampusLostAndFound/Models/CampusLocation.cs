using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CampusLostAndFound.Models
{
    public class CampusLocation
    {
        [Key]
        public int LocationId { get; set; }

        [Required]
        [StringLength(150)]
        public string BuildingName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? BlockName { get; set; }

        [StringLength(50)]
        public string? Floor { get; set; }

        [Required]
        [StringLength(150)]
        public string AreaName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

        public string DisplayName => string.IsNullOrWhiteSpace(BlockName)
            ? $"{BuildingName} - {AreaName} ({Floor ?? "Ground"})"
            : $"{BuildingName} (Block {BlockName}) - {AreaName} ({Floor ?? "Ground"})";
    }
}

