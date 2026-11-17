using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Controllers
{
    [Authorize(Roles = "SecurityPersonnel,CampusAdministrator,SystemAdministrator")]
    public class SecurityController : Controller
    {
        private readonly IClaimService _claimService;
        private readonly ICustodyService _custodyService;
        private readonly IItemService _itemService;
        private readonly IVerificationService _verificationService;

        public SecurityController(
            IClaimService claimService,
            ICustodyService custodyService,
            IItemService itemService,
            IVerificationService verificationService)
        {
            _claimService = claimService;
            _custodyService = custodyService;
            _itemService = itemService;
            _verificationService = verificationService;
        }

        public async Task<IActionResult> Index()
        {
            var pendingClaims = await _claimService.GetPendingClaimsAsync();
            var approvedForHandover = await _claimService.GetApprovedClaimsForHandoverAsync();
            var itemsInCustody = await _custodyService.GetItemsInCustodyAsync();
            var recentHandovers = await _custodyService.GetHandoverHistoryAsync();
            var unclaimedItems = await _itemService.GetUnclaimedItemsAsync();
            var stats = await _itemService.GetHomeStatisticsAsync();

            var model = new SecurityDashboardViewModel
            {
                ItemsInCustodyCount = itemsInCustody.Count,
                PendingClaimsCount = pendingClaims.Count,
                ApprovedAwaitingHandoverCount = approvedForHandover.Count,
                TotalReturnedCount = stats.TotalReturned,
                UnclaimedCount = unclaimedItems.Count,
                PendingClaims = pendingClaims.Take(5).ToList(),
                ApprovedClaimsForHandover = approvedForHandover.Take(5).ToList(),
                ItemsInCustody = itemsInCustody.Take(5).ToList(),
                RecentHandovers = recentHandovers.Take(5).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PendingClaims()
        {
            var claims = await _claimService.GetPendingClaimsAsync();
            return View(claims);
        }

        [HttpGet]
        public async Task<IActionResult> ReviewClaim(int claimId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var claim = await _claimService.GetClaimDetailsAsync(claimId, currentUserId, isPrivileged: true);
            if (claim == null) return NotFound();

            var (passed, score, suggested) = await _verificationService.EvaluateClaimVerificationAsync(claimId);
            ViewBag.AutomatedScore = score;
            ViewBag.SuggestedDecision = suggested;

            // Also load private details for side-by-side verification
            var itemDetails = await _itemService.GetItemDetailsAsync(claim.ItemId, currentUserId, isPrivileged: true);
            ViewBag.Item = itemDetails;

            var model = new ClaimReviewViewModel
            {
                ClaimId = claimId,
                Status = suggested,
                ReviewNotes = $"Verification score: {score}%. "
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewClaim(ClaimReviewViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(ReviewClaim), new { claimId = model.ClaimId });
            }

            string reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? reviewerEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, error) = await _claimService.ReviewClaimAsync(model, reviewerId, reviewerEmail);
            if (success)
            {
                TempData["SuccessMessage"] = $"Claim #{model.ClaimId} marked as {model.Status}.";
                return RedirectToAction(nameof(PendingClaims));
            }

            TempData["ErrorMessage"] = error ?? "Failed to update claim review.";
            return RedirectToAction(nameof(ReviewClaim), new { claimId = model.ClaimId });
        }

        [HttpGet]
        public async Task<IActionResult> CustodyList()
        {
            var items = await _custodyService.GetItemsInCustodyAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> AssignCustody(int itemId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var item = await _itemService.GetItemDetailsAsync(itemId, currentUserId, isPrivileged: true);
            if (item == null) return NotFound();

            var model = new CustodyAssignViewModel
            {
                ItemId = itemId,
                ItemTitle = item.Title,
                CustodyLocation = item.CurrentCustodyLocation ?? "Central Security Office Locker A-1"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCustody(CustodyAssignViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string securityOfficerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? officerEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, custodyId, error) = await _custodyService.AssignCustodyAsync(model, securityOfficerId, officerEmail);
            if (success)
            {
                TempData["SuccessMessage"] = $"Item custody logged successfully at '{model.CustodyLocation}'.";
                return RedirectToAction(nameof(CustodyList));
            }

            TempData["ErrorMessage"] = error ?? "Failed to log custody.";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Handover(int claimId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var claim = await _claimService.GetClaimDetailsAsync(claimId, currentUserId, isPrivileged: true);
            if (claim == null) return NotFound();

            if (claim.Status != ClaimStatuses.Approved)
            {
                TempData["ErrorMessage"] = "Only approved claims can proceed to supervised handover.";
                return RedirectToAction(nameof(PendingClaims));
            }

            var model = new HandoverCreateViewModel
            {
                ClaimId = claim.ClaimId,
                ItemId = claim.ItemId,
                ItemTitle = claim.ItemTitle,
                ClaimantName = claim.ClaimantName,
                ClaimantEmail = claim.ClaimantEmail,
                ClaimantRegNo = claim.ClaimantRegNo,
                ClaimantPhone = claim.ClaimantPhone,
                ProofReference = $"Verified Student ID: {claim.ClaimantRegNo ?? "N/A"}"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Handover(HandoverCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string securityOfficerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            string? officerEmail = User.FindFirstValue(ClaimTypes.Email);

            var (success, handoverId, error) = await _custodyService.CompleteSupervisedHandoverAsync(model, securityOfficerId, officerEmail);
            if (success)
            {
                TempData["SuccessMessage"] = $"Supervised handover completed! Item status updated to Returned and custody released.";
                return RedirectToAction(nameof(HandoverHistory));
            }

            TempData["ErrorMessage"] = error ?? "Handover could not be completed.";
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> HandoverHistory()
        {
            var history = await _custodyService.GetHandoverHistoryAsync();
            return View(history);
        }
    }
}

