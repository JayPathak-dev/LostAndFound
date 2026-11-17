using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CampusLostAndFound.ViewModels
{
    public class ItemCreateViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
        [Display(Name = "Item Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(2500, MinimumLength = 10, ErrorMessage = "Description must provide at least 10 characters.")]
        [Display(Name = "Public Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "Found"; // Lost or Found

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Item Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please select the campus location.")]
        [Display(Name = "Campus Location")]
        public int LocationId { get; set; }

        [Required(ErrorMessage = "Date and time of incident are required.")]
        [Display(Name = "Date and Time")]
        public DateTime ItemDateTime { get; set; } = DateTime.Now;

        [Display(Name = "Upload Photograph (Optional)")]
        public IFormFile? ImageFile { get; set; }

        // ================= PRIVATE DETAILS (Only for Found Items) =================
        [Display(Name = "Distinctive Marks or Scratches")]
        [StringLength(1000)]
        public string? DistinctiveMarks { get; set; }

        [Display(Name = "Hidden Features (e.g., secret pockets, specific engravings)")]
        [StringLength(1000)]
        public string? HiddenFeatures { get; set; }

        [Display(Name = "Internal Contents (e.g., items inside wallet or bag)")]
        [StringLength(1000)]
        public string? InternalContents { get; set; }

        [Display(Name = "Additional Identifiers (e.g., serial number suffix, stickers)")]
        [StringLength(1000)]
        public string? AdditionalIdentifiers { get; set; }

        // Dropdown selection lists
        public IEnumerable<SelectListItem>? Categories { get; set; }
        public IEnumerable<SelectListItem>? Locations { get; set; }
    }

    public class ItemCardViewModel
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string LocationName { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime ItemDateTime { get; set; }
        public string? ImagePath { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ItemDetailsViewModel
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public DateTime ItemDateTime { get; set; }
        public string? ImagePath { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ReporterName { get; set; } = string.Empty;
        public string ReporterId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Permission flags
        public bool IsOwner { get; set; }
        public bool IsPrivileged { get; set; } // Security or Admin
        public bool HasActiveClaimByCurrentUser { get; set; }
        public int? CurrentUserClaimId { get; set; }

        // Confidential verification details (populated ONLY if IsOwner or IsPrivileged)
        public PrivateItemDetailViewModel? PrivateDetails { get; set; }

        // Active custody information
        public string? CurrentCustodyLocation { get; set; }
        public string? CustodyOfficerName { get; set; }
        public DateTime? CustodyReceivedAt { get; set; }
    }

    public class PrivateItemDetailViewModel
    {
        public string? DistinctiveMarks { get; set; }
        public string? HiddenFeatures { get; set; }
        public string? InternalContents { get; set; }
        public string? AdditionalIdentifiers { get; set; }
    }

    public class ItemSearchViewModel
    {
        [Display(Name = "Search Keywords")]
        public string? SearchTerm { get; set; }

        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        [Display(Name = "Campus Location")]
        public int? LocationId { get; set; }

        [Display(Name = "Report Type")]
        public string ReportType { get; set; } = "Found"; // Found by default on public board

        [Display(Name = "From Date")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "To Date")]
        public DateTime? EndDate { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 9;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        public List<ItemCardViewModel> Items { get; set; } = new();

        public IEnumerable<SelectListItem>? Categories { get; set; }
        public IEnumerable<SelectListItem>? Locations { get; set; }
    }
}
