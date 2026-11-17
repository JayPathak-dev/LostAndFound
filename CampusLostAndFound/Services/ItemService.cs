using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public class ItemService : IItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly IMatchingService _matchingService;
        private readonly IAuditService _auditService;
        private readonly ILogger<ItemService> _logger;

        public ItemService(
            ApplicationDbContext context,
            IFileUploadService fileUploadService,
            IMatchingService matchingService,
            IAuditService auditService,
            ILogger<ItemService> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _matchingService = matchingService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<(bool Success, int ItemId, string? ErrorMessage)> CreateItemReportAsync(ItemCreateViewModel model, string userId, string? userEmail)
        {
            try
            {
                // 1. Validate Category and Location
                bool categoryExists = await _context.ItemCategories.AnyAsync(c => c.CategoryId == model.CategoryId && c.IsActive);
                if (!categoryExists)
                {
                    return (false, 0, "Selected category is invalid or inactive.");
                }

                bool locationExists = await _context.CampusLocations.AnyAsync(l => l.LocationId == model.LocationId && l.IsActive);
                if (!locationExists)
                {
                    return (false, 0, "Selected campus location is invalid or inactive.");
                }

                // 2. Handle Image Upload
                string? imagePath = null;
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var uploadResult = await _fileUploadService.UploadItemImageAsync(model.ImageFile);
                    if (!uploadResult.Success)
                    {
                        return (false, 0, uploadResult.ErrorMessage);
                    }
                    imagePath = uploadResult.FilePath;
                }

                // 3. Create Item Entity
                var item = new Item
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),
                    ReportType = model.ReportType == ReportTypes.Lost ? ReportTypes.Lost : ReportTypes.Found,
                    CategoryId = model.CategoryId,
                    LocationId = model.LocationId,
                    ItemDateTime = model.ItemDateTime,
                    ImagePath = imagePath,
                    Status = model.ReportType == ReportTypes.Lost ? ItemStatuses.Reported : ItemStatuses.Active,
                    ReporterId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Items.AddAsync(item);
                await _context.SaveChangesAsync();

                // 4. If Found Item, save Private Details separately
                if (item.ReportType == ReportTypes.Found &&
                    (!string.IsNullOrWhiteSpace(model.DistinctiveMarks) ||
                     !string.IsNullOrWhiteSpace(model.HiddenFeatures) ||
                     !string.IsNullOrWhiteSpace(model.InternalContents) ||
                     !string.IsNullOrWhiteSpace(model.AdditionalIdentifiers)))
                {
                    var privateDetail = new PrivateItemDetail
                    {
                        ItemId = item.ItemId,
                        DistinctiveMarks = model.DistinctiveMarks?.Trim(),
                        HiddenFeatures = model.HiddenFeatures?.Trim(),
                        InternalContents = model.InternalContents?.Trim(),
                        AdditionalIdentifiers = model.AdditionalIdentifiers?.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.PrivateItemDetails.AddAsync(privateDetail);
                    await _context.SaveChangesAsync();
                }

                // 5. Audit Log
                await _auditService.LogActionAsync(
                    userId,
                    userEmail,
                    $"Reported{item.ReportType}Item",
                    "Item",
                    item.ItemId.ToString(),
                    $"Created {item.ReportType} item report: '{item.Title}' in category {model.CategoryId}.");

                // 6. Trigger Matching Engine asynchronously
                try
                {
                    await _matchingService.FindAndSaveMatchesForReportAsync(item.ItemId);
                }
                catch (Exception matchEx)
                {
                    _logger.LogWarning(matchEx, "Failed to run matching engine immediately for item {ItemId}", item.ItemId);
                }

                return (true, item.ItemId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item report for user {UserId}", userId);
                return (false, 0, "An error occurred while saving your report. Please check your inputs and try again.");
            }
        }

        public async Task<ItemSearchViewModel> GetPublicFoundItemsAsync(ItemSearchViewModel searchModel)
        {
            var query = _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Where(i => i.ReportType == ReportTypes.Found && i.Status == ItemStatuses.Active)
                .AsNoTracking();

            // Keyword Search
            if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
            {
                string term = searchModel.SearchTerm.Trim().ToLower();
                query = query.Where(i => i.Title.ToLower().Contains(term) || i.Description.ToLower().Contains(term));
            }

            // Category Filter
            if (searchModel.CategoryId.HasValue && searchModel.CategoryId.Value > 0)
            {
                query = query.Where(i => i.CategoryId == searchModel.CategoryId.Value);
            }

            // Location Filter
            if (searchModel.LocationId.HasValue && searchModel.LocationId.Value > 0)
            {
                query = query.Where(i => i.LocationId == searchModel.LocationId.Value);
            }

            // Date Range Filters
            if (searchModel.StartDate.HasValue)
            {
                query = query.Where(i => i.ItemDateTime >= searchModel.StartDate.Value);
            }
            if (searchModel.EndDate.HasValue)
            {
                query = query.Where(i => i.ItemDateTime <= searchModel.EndDate.Value.AddDays(1));
            }

            searchModel.TotalItems = await query.CountAsync();

            int skip = (searchModel.PageNumber - 1) * searchModel.PageSize;
            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip(skip)
                .Take(searchModel.PageSize)
                .Select(i => new ItemCardViewModel
                {
                    ItemId = i.ItemId,
                    Title = i.Title,
                    Description = i.Description,
                    CategoryName = i.Category != null ? i.Category.CategoryName : "General",
                    LocationName = i.Location != null ? i.Location.DisplayName : "Campus",
                    BuildingName = i.Location != null ? i.Location.BuildingName : "Campus",
                    ItemDateTime = i.ItemDateTime,
                    ImagePath = i.ImagePath,
                    Status = i.Status,
                    ReportType = i.ReportType,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();

            searchModel.Items = items;
            return searchModel;
        }

        public async Task<ItemDetailsViewModel?> GetItemDetailsAsync(int itemId, string? currentUserId, bool isPrivileged)
        {
            var item = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Include(i => i.Reporter)
                .Include(i => i.PrivateDetail)
                .Include(i => i.CustodyRecords)
                    .ThenInclude(c => c.ResponsibleSecurityUser)
                .Include(i => i.Claims)
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (item == null) return null;

            bool isOwner = !string.IsNullOrEmpty(currentUserId) && item.ReporterId == currentUserId;
            var userClaim = !string.IsNullOrEmpty(currentUserId)
                ? item.Claims.FirstOrDefault(c => c.ClaimantId == currentUserId && c.Status != ClaimStatuses.Rejected)
                : null;

            var activeCustody = item.CustodyRecords
                .OrderByDescending(c => c.ReceivedAt)
                .FirstOrDefault(c => c.ReleasedAt == null);

            var model = new ItemDetailsViewModel
            {
                ItemId = item.ItemId,
                Title = item.Title,
                Description = item.Description,
                ReportType = item.ReportType,
                CategoryName = item.Category?.CategoryName ?? "General",
                CategoryId = item.CategoryId,
                LocationName = item.Location?.DisplayName ?? "Campus",
                BuildingName = item.Location?.BuildingName ?? "Campus",
                ItemDateTime = item.ItemDateTime,
                ImagePath = item.ImagePath,
                Status = item.Status,
                ReporterName = item.Reporter?.FullName ?? "Campus Member",
                ReporterId = item.ReporterId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                IsOwner = isOwner,
                IsPrivileged = isPrivileged,
                HasActiveClaimByCurrentUser = userClaim != null,
                CurrentUserClaimId = userClaim?.ClaimId,
                CurrentCustodyLocation = activeCustody?.CustodyLocation,
                CustodyOfficerName = activeCustody?.ResponsibleSecurityUser?.FullName,
                CustodyReceivedAt = activeCustody?.ReceivedAt
            };

            // STRICT PRIVACY RULE: Populate private details ONLY for the original reporter or authorized staff!
            if ((isOwner || isPrivileged) && item.PrivateDetail != null)
            {
                model.PrivateDetails = new PrivateItemDetailViewModel
                {
                    DistinctiveMarks = item.PrivateDetail.DistinctiveMarks,
                    HiddenFeatures = item.PrivateDetail.HiddenFeatures,
                    InternalContents = item.PrivateDetail.InternalContents,
                    AdditionalIdentifiers = item.PrivateDetail.AdditionalIdentifiers
                };
            }

            return model;
        }

        public async Task<List<ItemCardViewModel>> GetUserReportsAsync(string userId)
        {
            return await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Where(i => i.ReporterId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new ItemCardViewModel
                {
                    ItemId = i.ItemId,
                    Title = i.Title,
                    Description = i.Description,
                    CategoryName = i.Category != null ? i.Category.CategoryName : "General",
                    LocationName = i.Location != null ? i.Location.DisplayName : "Campus",
                    BuildingName = i.Location != null ? i.Location.BuildingName : "Campus",
                    ItemDateTime = i.ItemDateTime,
                    ImagePath = i.ImagePath,
                    Status = i.Status,
                    ReportType = i.ReportType,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<ItemCardViewModel>> GetRecentFoundItemsAsync(int count = 6)
        {
            return await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Where(i => i.ReportType == ReportTypes.Found && i.Status == ItemStatuses.Active)
                .OrderByDescending(i => i.CreatedAt)
                .Take(count)
                .Select(i => new ItemCardViewModel
                {
                    ItemId = i.ItemId,
                    Title = i.Title,
                    Description = i.Description,
                    CategoryName = i.Category != null ? i.Category.CategoryName : "General",
                    LocationName = i.Location != null ? i.Location.DisplayName : "Campus",
                    BuildingName = i.Location != null ? i.Location.BuildingName : "Campus",
                    ItemDateTime = i.ItemDateTime,
                    ImagePath = i.ImagePath,
                    Status = i.Status,
                    ReportType = i.ReportType,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<(int TotalLost, int TotalFound, int TotalReturned, int PendingClaims)> GetHomeStatisticsAsync()
        {
            int totalLost = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Lost);
            int totalFound = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Found);
            int totalReturned = await _context.Items.CountAsync(i => i.Status == ItemStatuses.Returned);
            int pendingClaims = await _context.Claims.CountAsync(c => c.Status == ClaimStatuses.Pending || c.Status == ClaimStatuses.UnderVerification);

            return (totalLost, totalFound, totalReturned, pendingClaims);
        }

        public async Task<List<ItemCategory>> GetCategoriesAsync(bool activeOnly = true)
        {
            var query = _context.ItemCategories.AsNoTracking();
            if (activeOnly)
            {
                query = query.Where(c => c.IsActive);
            }
            return await query.OrderBy(c => c.CategoryName).ToListAsync();
        }

        public async Task<List<CampusLocation>> GetLocationsAsync(bool activeOnly = true)
        {
            var query = _context.CampusLocations.AsNoTracking();
            if (activeOnly)
            {
                query = query.Where(l => l.IsActive);
            }
            return await query.OrderBy(l => l.BuildingName).ThenBy(l => l.AreaName).ToListAsync();
        }

        public async Task<List<UnclaimedItemViewModel>> GetUnclaimedItemsAsync()
        {
            var now = DateTime.UtcNow;

            var items = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Where(i => i.ReportType == ReportTypes.Found &&
                           (i.Status == ItemStatuses.Active || i.Status == ItemStatuses.Unclaimed))
                .OrderBy(i => i.ItemDateTime)
                .ToListAsync();

            return items.Select(i =>
            {
                int retention = i.Category?.RetentionPeriodDays ?? 60;
                int daysInCustody = (int)(now - i.ItemDateTime).TotalDays;

                return new UnclaimedItemViewModel
                {
                    ItemId = i.ItemId,
                    Title = i.Title,
                    CategoryName = i.Category?.CategoryName ?? "General",
                    LocationName = i.Location?.DisplayName ?? "Campus",
                    FoundDate = i.ItemDateTime,
                    DaysInCustody = daysInCustody,
                    RetentionPeriodDays = retention,
                    CurrentStatus = i.Status
                };
            }).ToList();
        }

        public async Task<bool> UpdateItemStatusAsync(int itemId, string newStatus, string actorId, string? reason)
        {
            var item = await _context.Items.FindAsync(itemId);
            if (item == null || !ItemStatuses.All.Contains(newStatus))
            {
                return false;
            }

            string previousStatus = item.Status;
            item.Status = newStatus;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                actorId,
                null,
                "ItemStatusChanged",
                "Item",
                item.ItemId.ToString(),
                $"Status changed from '{previousStatus}' to '{newStatus}'. Reason: {reason ?? "N/A"}");

            return true;
        }
    }
}
