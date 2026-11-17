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
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IItemService _itemService;
        private readonly IClaimService _claimService;
        private readonly IMatchingService _matchingService;

        public DashboardController(
            IItemService itemService,
            IClaimService claimService,
            IMatchingService matchingService)
        {
            _itemService = itemService;
            _claimService = claimService;
            _matchingService = matchingService;
        }

        public async Task<IActionResult> Index()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var reports = await _itemService.GetUserReportsAsync(userId);
            var claims = await _claimService.GetUserClaimsAsync(userId);
            var matches = await _matchingService.GetMatchesForUserAsync(userId);

            int totalLost = reports.Count(r => r.ReportType == ReportTypes.Lost);
            int totalFound = reports.Count(r => r.ReportType == ReportTypes.Found);
            int activeClaims = claims.Count(c => c.Status != ClaimStatuses.Rejected);
            int recovered = claims.Count(c => c.Status == ClaimStatuses.Approved || c.Status == ItemStatuses.Returned) +
                            reports.Count(r => r.Status == ItemStatuses.Returned && r.ReportType == ReportTypes.Lost);

            var model = new UserDashboardViewModel
            {
                TotalLostReports = totalLost,
                TotalFoundReports = totalFound,
                ActiveClaimsCount = activeClaims,
                ItemsRecoveredCount = recovered,
                MyRecentReports = reports.Take(5).ToList(),
                MyRecentClaims = claims.Take(5).ToList(),
                MatchSuggestions = matches.Take(6).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> MyReports()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var reports = await _itemService.GetUserReportsAsync(userId);
            return View(reports);
        }

        public async Task<IActionResult> MyClaims()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var claims = await _claimService.GetUserClaimsAsync(userId);
            return View(claims);
        }

        public async Task<IActionResult> Matches()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var matches = await _matchingService.GetMatchesForUserAsync(userId);
            return View(matches);
        }
    }
}

