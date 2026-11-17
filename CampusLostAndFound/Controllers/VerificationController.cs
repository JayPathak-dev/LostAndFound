using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Controllers
{
    [Authorize]
    public class VerificationController : Controller
    {
        private readonly IVerificationService _verificationService;
        private readonly IClaimService _claimService;

        public VerificationController(
            IVerificationService verificationService,
            IClaimService claimService)
        {
            _verificationService = verificationService;
            _claimService = claimService;
        }

        [HttpGet]
        public async Task<IActionResult> Challenge(int claimId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool isPrivileged = User.IsInRole("SecurityPersonnel") || User.IsInRole("CampusAdministrator") || User.IsInRole("SystemAdministrator");

            var claim = await _claimService.GetClaimDetailsAsync(claimId, currentUserId, isPrivileged);
            if (claim == null)
            {
                return NotFound();
            }

            var challenges = await _verificationService.GenerateChallengesForClaimAsync(claimId);
            if (!challenges.Any())
            {
                TempData["InfoMessage"] = "No additional confidential verification questions were required for this item. Your claim is in queue for review.";
                return RedirectToAction("Details", "Claims", new { id = claimId });
            }

            ViewBag.Claim = claim;
            return View(challenges);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Challenge(int claimId, List<ChallengeAnswerInputModel> answers)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            if (answers == null || !answers.Any())
            {
                TempData["ErrorMessage"] = "Please provide answers to the verification questions.";
                return RedirectToAction(nameof(Challenge), new { claimId });
            }

            bool success = await _verificationService.SubmitVerificationAnswersAsync(claimId, answers, currentUserId);
            if (!success)
            {
                TempData["ErrorMessage"] = "Failed to process verification answers. Please try again.";
                return RedirectToAction(nameof(Challenge), new { claimId });
            }

            var (passed, score, suggested) = await _verificationService.EvaluateClaimVerificationAsync(claimId);

            TempData["SuccessMessage"] = $"Your verification answers have been recorded (Confidence Score: {score}%). Our campus security team will review your claim.";
            return RedirectToAction("Details", "Claims", new { id = claimId });
        }
    }
}
