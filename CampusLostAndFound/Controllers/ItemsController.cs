using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Controllers
{
    public class ItemsController : Controller
    {
        private readonly IItemService _itemService;
        private readonly IMatchingService _matchingService;

        public ItemsController(IItemService itemService, IMatchingService matchingService)
        {
            _itemService = itemService;
            _matchingService = matchingService;
        }

        // Public Item Board
        [HttpGet]
        public async Task<IActionResult> Index(ItemSearchViewModel searchModel)
        {
            var categories = await _itemService.GetCategoriesAsync(activeOnly: true);
            var locations = await _itemService.GetLocationsAsync(activeOnly: true);

            searchModel.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName,
                Selected = searchModel.CategoryId == c.CategoryId
            });

            searchModel.Locations = locations.Select(l => new SelectListItem
            {
                Value = l.LocationId.ToString(),
                Text = l.DisplayName,
                Selected = searchModel.LocationId == l.LocationId
            });

            var resultModel = await _itemService.GetPublicFoundItemsAsync(searchModel);
            resultModel.Categories = searchModel.Categories;
            resultModel.Locations = searchModel.Locations;

            return View(resultModel);
        }

        // Item Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isPrivileged = User.IsInRole("SecurityPersonnel") ||
                               User.IsInRole("CampusAdministrator") ||
                               User.IsInRole("SystemAdministrator");

            var item = await _itemService.GetItemDetailsAsync(id, currentUserId, isPrivileged);
            if (item == null)
            {
                return NotFound();
            }

            // Also fetch potential match suggestions for this item if owner or privileged
            if (item.IsOwner || isPrivileged)
            {
                ViewBag.Matches = await _matchingService.GetMatchesForItemAsync(id);
            }

            return View(item);
        }

        // Report Lost Item
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ReportLost()
        {
            var model = new ItemCreateViewModel
            {
                ReportType = ReportTypes.Lost,
                ItemDateTime = DateTime.Now
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportLost(ItemCreateViewModel model)
        {
            model.ReportType = ReportTypes.Lost;

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, itemId, error) = await _itemService.CreateItemReportAsync(model, userId, userEmail);
            if (success)
            {
                TempData["SuccessMessage"] = "Your lost item report has been logged and our matching system is searching for corresponding found belongings!";
                return RedirectToAction(nameof(Details), new { id = itemId });
            }

            ModelState.AddModelError(string.Empty, error ?? "Unable to submit report.");
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        // Report Found Item
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ReportFound()
        {
            var model = new ItemCreateViewModel
            {
                ReportType = ReportTypes.Found,
                ItemDateTime = DateTime.Now
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportFound(ItemCreateViewModel model)
        {
            model.ReportType = ReportTypes.Found;

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, itemId, error) = await _itemService.CreateItemReportAsync(model, userId, userEmail);
            if (success)
            {
                TempData["SuccessMessage"] = "Found item recorded successfully! Sensitive private details are securely stored and hidden from the public board.";
                return RedirectToAction(nameof(Details), new { id = itemId });
            }

            ModelState.AddModelError(string.Empty, error ?? "Unable to submit found item report.");
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        private async Task PopulateDropdownsAsync(ItemCreateViewModel model)
        {
            var categories = await _itemService.GetCategoriesAsync(activeOnly: true);
            var locations = await _itemService.GetLocationsAsync(activeOnly: true);

            model.Categories = categories.Select(c => new SelectListItem
            {
                Value = c.CategoryId.ToString(),
                Text = c.CategoryName
            });

            model.Locations = locations.Select(l => new SelectListItem
            {
                Value = l.LocationId.ToString(),
                Text = l.DisplayName
            });
        }
    }
}

