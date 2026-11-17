using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly IClaimService _claimService;
        private readonly IItemService _itemService;
        private readonly IVerificationService _verificationService;

        public ClaimsController(
            IClaimService claimService,
            IItemService itemService,
            IVerificationService verificationService)
        {
            _claimService = claimService;
            _itemService = itemService;
            _verificationService = verificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int itemId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool isPrivileged = User.IsInRole("SecurityPersonnel") || User.IsInRole("CampusAdministrator") || User.IsInRole("SystemAdministrator");

            var item = await _itemService.GetItemDetailsAsync(itemId, currentUserId, isPrivileged);
            if (item == null)
            {
                return NotFound();
            }

            if (item.Status == ItemStatuses.Returned || item.Status == ItemStatuses.Disposed)
            {
                TempData["ErrorMessage"] = $"This item has already been {item.Status} and cannot be claimed.";
                return RedirectToAction("Details", "Items", new { id = itemId });
            }

            if (item.ReporterId == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot claim an item that you reported.";
                return RedirectToAction("Details", "Items", new { id = itemId });
            }

            if (item.HasActiveClaimByCurrentUser)
            {
                TempData["InfoMessage"] = "You already have an active claim for this item.";
                return RedirectToAction(nameof(Details), new { id = item.CurrentUserClaimId });
            }

            var model = new ClaimCreateViewModel
            {
                ItemId = item.ItemId,
                ItemTitle = item.Title,
                ItemCategory = item.CategoryName,
                ItemLocation = item.LocationName,
                FoundDateTime = item.ItemDateTime,
                ItemImagePath = item.ImagePath
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClaimCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? userEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, claimId, error) = await _claimService.SubmitClaimAsync(model, currentUserId, userEmail);
            if (success)
            {
                TempData["SuccessMessage"] = "Your ownership claim has been submitted! Please proceed to complete the verification challenge questions.";
                return RedirectToAction("Challenge", "Verification", new { claimId = claimId });
            }

            TempData["ErrorMessage"] = error ?? "Failed to submit claim.";
            return RedirectToAction("Details", "Items", new { id = model.ItemId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool isPrivileged = User.IsInRole("SecurityPersonnel") || User.IsInRole("CampusAdministrator") || User.IsInRole("SystemAdministrator");

            var claim = await _claimService.GetClaimDetailsAsync(id, currentUserId, isPrivileged);
            if (claim == null)
            {
                return NotFound();
            }

            return View(claim);
        }
    }
}

