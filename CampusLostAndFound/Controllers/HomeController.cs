using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;

namespace CampusLostAndFound.Controllers
{
    public class HomeController : Controller
    {
        private readonly IItemService _itemService;

        public HomeController(IItemService itemService)
        {
            _itemService = itemService;
        }

        public async Task<IActionResult> Index()
        {
            var stats = await _itemService.GetHomeStatisticsAsync();
            var recentFound = await _itemService.GetRecentFoundItemsAsync(6);

            ViewBag.TotalLost = stats.TotalLost;
            ViewBag.TotalFound = stats.TotalFound;
            ViewBag.TotalReturned = stats.TotalReturned;
            ViewBag.PendingClaims = stats.PendingClaims;

            return View(recentFound);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
