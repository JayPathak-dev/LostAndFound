using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public interface ICustodyService
    {
        Task<(bool Success, int CustodyId, string? ErrorMessage)> AssignCustodyAsync(CustodyAssignViewModel model, string securityOfficerId, string? officerEmail);
        Task<List<CustodyItemViewModel>> GetItemsInCustodyAsync();
        Task<(bool Success, int HandoverId, string? ErrorMessage)> CompleteSupervisedHandoverAsync(HandoverCreateViewModel model, string securityOfficerId, string? officerEmail);
        Task<List<HandoverRecordViewModel>> GetHandoverHistoryAsync();
        Task<(bool Success, int DisposalId, string? ErrorMessage)> RecordDisposalAsync(DisposalCreateViewModel model, string authorizedUserId, string? userEmail);
    }
}
