using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Controllers
{
    [Authorize(Roles = "CampusAdministrator,SystemAdministrator")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IItemService _itemService;
        private readonly ICustodyService _custodyService;
        private readonly IAuditService _auditService;

        public AdminController(
            ApplicationDbContext context,
            IItemService itemService,
            ICustodyService custodyService,
            IAuditService auditService)
        {
            _context = context;
            _itemService = itemService;
            _custodyService = custodyService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index()
        {
            int totalLost = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Lost);
            int totalFound = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Found);
            int totalReturned = await _context.Items.CountAsync(i => i.Status == ItemStatuses.Returned);
            int pendingClaims = await _context.Claims.CountAsync(c => c.Status == ClaimStatuses.Pending || c.Status == ClaimStatuses.UnderVerification);
            int itemsInCustody = await _context.CustodyRecords.CountAsync(c => c.ReleasedAt == null && c.Item!.Status != ItemStatuses.Returned && c.Item.Status != ItemStatuses.Disposed);
            int disposedCount = await _context.DisposalRecords.CountAsync();

            var unclaimedItems = await _itemService.GetUnclaimedItemsAsync();
            var recentAudits = await _auditService.GetRecentLogsAsync(15);

            double recoveryRate = totalLost + totalFound > 0
                ? Math.Round(((double)totalReturned / (totalLost + totalFound)) * 100, 1)
                : 0.0;

            var categoryCounts = await _context.Items
                .Include(i => i.Category)
                .GroupBy(i => i.Category != null ? i.Category.CategoryName : "Uncategorized")
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Category, v => v.Count);

            var locationCounts = await _context.Items
                .Include(i => i.Location)
                .GroupBy(i => i.Location != null ? i.Location.BuildingName : "Unknown")
                .Select(g => new { Building = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Building, v => v.Count);

            var model = new AdminDashboardViewModel
            {
                TotalLost = totalLost,
                TotalFound = totalFound,
                TotalReturned = totalReturned,
                PendingClaims = pendingClaims,
                ItemsInCustody = itemsInCustody,
                UnclaimedCount = unclaimedItems.Count,
                DisposedCount = disposedCount,
                RecoveryRatePercentage = recoveryRate,
                CategoryBreakdown = categoryCounts,
                LocationBreakdown = locationCounts,
                RecentAuditLogs = recentAudits,
                UnclaimedItems = unclaimedItems.Take(5).ToList()
            };

            return View(model);
        }

        // ================= CATEGORIES =================
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.ItemCategories.OrderBy(c => c.CategoryName).ToListAsync();
            return View(categories);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            return View(new CategoryFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var category = new ItemCategory
            {
                CategoryName = model.CategoryName.Trim(),
                Description = model.Description?.Trim(),
                RetentionPeriodDays = model.RetentionPeriodDays,
                IsActive = model.IsActive
            };

            await _context.ItemCategories.AddAsync(category);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Email),
                "CategoryCreated",
                "ItemCategory",
                category.CategoryId.ToString(),
                $"Created category '{category.CategoryName}' (Retention: {category.RetentionPeriodDays} days).");

            TempData["SuccessMessage"] = $"Category '{category.CategoryName}' created successfully.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _context.ItemCategories.FindAsync(id);
            if (category == null) return NotFound();

            var model = new CategoryFormViewModel
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Description = category.Description,
                RetentionPeriodDays = category.RetentionPeriodDays,
                IsActive = category.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var category = await _context.ItemCategories.FindAsync(model.CategoryId);
            if (category == null) return NotFound();

            category.CategoryName = model.CategoryName.Trim();
            category.Description = model.Description?.Trim();
            category.RetentionPeriodDays = model.RetentionPeriodDays;
            category.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Email),
                "CategoryUpdated",
                "ItemCategory",
                category.CategoryId.ToString(),
                $"Updated category '{category.CategoryName}'.");

            TempData["SuccessMessage"] = "Category updated successfully.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCategory(int id)
        {
            var category = await _context.ItemCategories.FindAsync(id);
            if (category == null) return NotFound();

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category '{category.CategoryName}' is now {(category.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Categories));
        }

        // ================= LOCATIONS =================
        [HttpGet]
        public async Task<IActionResult> Locations()
        {
            var locations = await _context.CampusLocations.OrderBy(l => l.BuildingName).ThenBy(l => l.AreaName).ToListAsync();
            return View(locations);
        }

        [HttpGet]
        public IActionResult CreateLocation()
        {
            return View(new LocationFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation(LocationFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var location = new CampusLocation
            {
                BuildingName = model.BuildingName.Trim(),
                BlockName = model.BlockName?.Trim(),
                Floor = model.Floor?.Trim(),
                AreaName = model.AreaName.Trim(),
                Description = model.Description?.Trim(),
                IsActive = model.IsActive
            };

            await _context.CampusLocations.AddAsync(location);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Email),
                "LocationCreated",
                "CampusLocation",
                location.LocationId.ToString(),
                $"Created campus location '{location.DisplayName}'.");

            TempData["SuccessMessage"] = "Campus location added successfully.";
            return RedirectToAction(nameof(Locations));
        }

        [HttpGet]
        public async Task<IActionResult> EditLocation(int id)
        {
            var location = await _context.CampusLocations.FindAsync(id);
            if (location == null) return NotFound();

            var model = new LocationFormViewModel
            {
                LocationId = location.LocationId,
                BuildingName = location.BuildingName,
                BlockName = location.BlockName,
                Floor = location.Floor,
                AreaName = location.AreaName,
                Description = location.Description,
                IsActive = location.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLocation(LocationFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var location = await _context.CampusLocations.FindAsync(model.LocationId);
            if (location == null) return NotFound();

            location.BuildingName = model.BuildingName.Trim();
            location.BlockName = model.BlockName?.Trim();
            location.Floor = model.Floor?.Trim();
            location.AreaName = model.AreaName.Trim();
            location.Description = model.Description?.Trim();
            location.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Campus location updated successfully.";
            return RedirectToAction(nameof(Locations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLocation(int id)
        {
            var location = await _context.CampusLocations.FindAsync(id);
            if (location == null) return NotFound();

            location.IsActive = !location.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Location '{location.DisplayName}' is now {(location.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Locations));
        }

        // ================= RETENTION & UNCLAIMED DISPOSAL =================
        [HttpGet]
        public async Task<IActionResult> Retention()
        {
            var categories = await _context.ItemCategories.OrderBy(c => c.CategoryName).ToListAsync();
            var unclaimed = await _itemService.GetUnclaimedItemsAsync();

            ViewBag.Unclaimed = unclaimed;
            return View(categories);
        }

        [HttpGet]
        public async Task<IActionResult> Unclaimed()
        {
            var items = await _itemService.GetUnclaimedItemsAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> DisposeItem(int itemId)
        {
            var item = await _context.Items
                .Include(i => i.Category)
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (item == null) return NotFound();

            int days = (int)(DateTime.UtcNow - item.ItemDateTime).TotalDays;

            var model = new DisposalCreateViewModel
            {
                ItemId = item.ItemId,
                ItemTitle = item.Title,
                CategoryName = item.Category?.CategoryName ?? "General",
                DaysInCustody = days,
                DisposalReason = "Item unclaimed beyond campus retention policy period. Donated to university welfare board."
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisposeItem(DisposalCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, disposalId, error) = await _custodyService.RecordDisposalAsync(model, userId, userEmail);
            if (success)
            {
                TempData["SuccessMessage"] = "Item disposal record logged successfully. Item marked as Disposed.";
                return RedirectToAction(nameof(Unclaimed));
            }

            TempData["ErrorMessage"] = error ?? "Failed to record disposal.";
            return View(model);
        }

        // ================= AUDIT LOGS =================
        [HttpGet]
        public async Task<IActionResult> AuditLogs(string? targetRecord = null)
        {
            var query = _context.AuditEntries.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(targetRecord))
            {
                query = query.Where(a => a.TargetRecord == targetRecord);
            }

            var logs = await query.OrderByDescending(a => a.Timestamp).Take(100).ToListAsync();
            ViewBag.SelectedRecord = targetRecord;
            return View(logs);
        }

        // ================= REPORTS =================
        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var totalLost = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Lost);
            var totalFound = await _context.Items.CountAsync(i => i.ReportType == ReportTypes.Found);
            var totalReturned = await _context.Items.CountAsync(i => i.Status == ItemStatuses.Returned);
            var totalDisposed = await _context.DisposalRecords.CountAsync();

            var categoryCounts = await _context.Items
                .Include(i => i.Category)
                .GroupBy(i => i.Category != null ? i.Category.CategoryName : "Uncategorized")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            var locationCounts = await _context.Items
                .Include(i => i.Location)
                .GroupBy(i => i.Location != null ? i.Location.BuildingName : "Unknown")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TotalLost = totalLost;
            ViewBag.TotalFound = totalFound;
            ViewBag.TotalReturned = totalReturned;
            ViewBag.TotalDisposed = totalDisposed;
            ViewBag.CategoryCounts = categoryCounts;
            ViewBag.LocationCounts = locationCounts;

            return View();
        }
    }
}
