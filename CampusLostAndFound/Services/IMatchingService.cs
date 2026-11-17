using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public interface IMatchingService
    {
        (double Score, string Reason) CalculateMatchScore(Item lostItem, Item foundItem);
        Task<int> FindAndSaveMatchesForReportAsync(int itemId);
        Task<List<MatchSuggestionCardViewModel>> GetMatchesForUserAsync(string userId);
        Task<List<MatchSuggestionCardViewModel>> GetMatchesForItemAsync(int itemId);
    }
}
