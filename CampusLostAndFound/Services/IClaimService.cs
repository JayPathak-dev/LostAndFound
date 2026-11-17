using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public interface IClaimService
    {
        Task<(bool Success, int ClaimId, string? ErrorMessage)> SubmitClaimAsync(ClaimCreateViewModel model, string claimantId, string? claimantEmail);
        Task<List<ClaimSummaryViewModel>> GetUserClaimsAsync(string claimantId);
        Task<List<ClaimDetailsViewModel>> GetPendingClaimsAsync();
        Task<List<ClaimDetailsViewModel>> GetApprovedClaimsForHandoverAsync();
        Task<ClaimDetailsViewModel?> GetClaimDetailsAsync(int claimId, string currentUserId, bool isPrivileged);
        Task<(bool Success, string? ErrorMessage)> ReviewClaimAsync(ClaimReviewViewModel model, string reviewerId, string? reviewerEmail);
    }
}
