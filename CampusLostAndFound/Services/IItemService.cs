using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public interface IItemService
    {
        Task<(bool Success, int ItemId, string? ErrorMessage)> CreateItemReportAsync(ItemCreateViewModel model, string userId, string? userEmail);
        Task<ItemSearchViewModel> GetPublicFoundItemsAsync(ItemSearchViewModel searchModel);
        Task<ItemDetailsViewModel?> GetItemDetailsAsync(int itemId, string? currentUserId, bool isPrivileged);
        Task<List<ItemCardViewModel>> GetUserReportsAsync(string userId);
        Task<List<ItemCardViewModel>> GetRecentFoundItemsAsync(int count = 6);
        Task<(int TotalLost, int TotalFound, int TotalReturned, int PendingClaims)> GetHomeStatisticsAsync();
        Task<List<ItemCategory>> GetCategoriesAsync(bool activeOnly = true);
        Task<List<CampusLocation>> GetLocationsAsync(bool activeOnly = true);
        Task<List<UnclaimedItemViewModel>> GetUnclaimedItemsAsync();
        Task<bool> UpdateItemStatusAsync(int itemId, string newStatus, string actorId, string? reason);
    }
}
