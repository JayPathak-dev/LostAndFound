using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Models;

namespace CampusLostAndFound.Services
{
    public interface IAuditService
    {
        Task LogActionAsync(string? actorId, string? actorEmail, string action, string targetRecord, string targetRecordId, string? additionalInfo = null);
        Task<List<AuditEntry>> GetRecentLogsAsync(int count = 50);
        Task<List<AuditEntry>> GetLogsForRecordAsync(string targetRecord, string targetRecordId);
    }
}
