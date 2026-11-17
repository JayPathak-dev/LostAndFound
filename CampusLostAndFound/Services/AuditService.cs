using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;

namespace CampusLostAndFound.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogActionAsync(string? actorId, string? actorEmail, string action, string targetRecord, string targetRecordId, string? additionalInfo = null)
        {
            try
            {
                var entry = new AuditEntry
                {
                    ActorId = actorId,
                    ActorEmail = actorEmail,
                    Action = action,
                    TargetRecord = targetRecord,
                    TargetRecordId = targetRecordId,
                    Timestamp = DateTime.UtcNow,
                    AdditionalInformation = additionalInfo
                };

                _context.AuditEntries.Add(entry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write audit entry for action {Action} on {TargetRecord}:{TargetRecordId}", action, targetRecord, targetRecordId);
            }
        }

        public async Task<List<AuditEntry>> GetRecentLogsAsync(int count = 50)
        {
            return await _context.AuditEntries
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<AuditEntry>> GetLogsForRecordAsync(string targetRecord, string targetRecordId)
        {
            return await _context.AuditEntries
                .Where(a => a.TargetRecord == targetRecord && a.TargetRecordId == targetRecordId)
                .OrderByDescending(a => a.Timestamp)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
