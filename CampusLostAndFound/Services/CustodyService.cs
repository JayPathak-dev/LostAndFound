using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public class CustodyService : ICustodyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ILogger<CustodyService> _logger;

        public CustodyService(
            ApplicationDbContext context,
            IAuditService auditService,
            ILogger<CustodyService> logger)
        {
            _context = context;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<(bool Success, int CustodyId, string? ErrorMessage)> AssignCustodyAsync(CustodyAssignViewModel model, string securityOfficerId, string? officerEmail)
        {
            try
            {
                var item = await _context.Items.FindAsync(model.ItemId);
                if (item == null)
                {
                    return (false, 0, "Item not found.");
                }

                // Check for existing active custody
                var existingActive = await _context.CustodyRecords
                    .FirstOrDefaultAsync(c => c.ItemId == model.ItemId && c.ReleasedAt == null);

                if (existingActive != null)
                {
                    existingActive.ReleasedAt = DateTime.UtcNow;
                }

                var custody = new CustodyRecord
                {
                    ItemId = model.ItemId,
                    CustodyLocation = model.CustodyLocation.Trim(),
                    ResponsibleSecurityUserId = securityOfficerId,
                    ReceivedAt = DateTime.UtcNow,
                    Notes = model.Notes?.Trim()
                };

                await _context.CustodyRecords.AddAsync(custody);
                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(
                    securityOfficerId,
                    officerEmail,
                    "CustodyAssigned",
                    "Item",
                    item.ItemId.ToString(),
                    $"Item #{item.ItemId} placed into custody at '{custody.CustodyLocation}'.");

                return (true, custody.CustodyId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign custody for item {ItemId}", model.ItemId);
                return (false, 0, "An error occurred while updating custody records.");
            }
        }

        public async Task<List<CustodyItemViewModel>> GetItemsInCustodyAsync()
        {
            var records = await _context.CustodyRecords
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Category)
                .Include(c => c.ResponsibleSecurityUser)
                .Where(c => c.ReleasedAt == null && c.Item!.Status != ItemStatuses.Returned && c.Item.Status != ItemStatuses.Disposed)
                .OrderByDescending(c => c.ReceivedAt)
                .ToListAsync();

            return records.Select(c => new CustodyItemViewModel
            {
                CustodyId = c.CustodyId,
                ItemId = c.ItemId,
                ItemTitle = c.Item?.Title ?? "Item",
                CategoryName = c.Item?.Category?.CategoryName ?? "General",
                CustodyLocation = c.CustodyLocation,
                SecurityOfficerName = c.ResponsibleSecurityUser?.FullName ?? "Security Officer",
                ReceivedAt = c.ReceivedAt,
                ItemStatus = c.Item?.Status ?? "Unknown",
                Notes = c.Notes
            }).ToList();
        }

        public async Task<(bool Success, int HandoverId, string? ErrorMessage)> CompleteSupervisedHandoverAsync(HandoverCreateViewModel model, string securityOfficerId, string? officerEmail)
        {
            // Execute in an isolated transaction for ACID compliance
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<(bool Success, int HandoverId, string? ErrorMessage)>(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var claim = await _context.Claims
                        .Include(c => c.Item)
                        .Include(c => c.Claimant)
                        .FirstOrDefaultAsync(c => c.ClaimId == model.ClaimId);

                    if (claim == null)
                    {
                        return (false, 0, "Claim not found.");
                    }

                    if (claim.Status != ClaimStatuses.Approved)
                    {
                        return (false, 0, $"Cannot complete handover because claim status is '{claim.Status}'. Claim must be Approved first.");
                    }

                    if (claim.Item == null)
                    {
                        return (false, 0, "Item reference missing.");
                    }

                    if (claim.Item.Status == ItemStatuses.Returned)
                    {
                        return (false, 0, "This item has already been marked as Returned.");
                    }

                    // Check if handover record already exists
                    bool alreadyHandedOver = await _context.Handovers.AnyAsync(h => h.ClaimId == model.ClaimId || h.ItemId == claim.ItemId);
                    if (alreadyHandedOver)
                    {
                        return (false, 0, "A handover record already exists for this claim or item.");
                    }

                    // 1. Create Handover entity
                    var handover = new Handover
                    {
                        ItemId = claim.ItemId,
                        ClaimId = claim.ClaimId,
                        ReceivingUserId = claim.ClaimantId,
                        SecurityUserId = securityOfficerId,
                        HandoverDateTime = DateTime.UtcNow,
                        ProofReference = model.ProofReference.Trim(),
                        Notes = model.Notes?.Trim()
                    };

                    await _context.Handovers.AddAsync(handover);

                    // 2. Update Item Status to Returned
                    claim.Item.Status = ItemStatuses.Returned;
                    claim.Item.UpdatedAt = DateTime.UtcNow;

                    // 3. Release any active custody records
                    var activeCustodies = await _context.CustodyRecords
                        .Where(c => c.ItemId == claim.ItemId && c.ReleasedAt == null)
                        .ToListAsync();

                    foreach (var custody in activeCustodies)
                    {
                        custody.ReleasedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // 4. Log Audit
                    await _auditService.LogActionAsync(
                        securityOfficerId,
                        officerEmail,
                        "ItemHandoverCompleted",
                        "Handover",
                        handover.HandoverId.ToString(),
                        $"Item #{claim.ItemId} ('{claim.Item.Title}') successfully handed over to {claim.Claimant?.FullName} (ID: {model.ProofReference}).");

                    return (true, handover.HandoverId, null);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed while completing handover for claim {ClaimId}", model.ClaimId);
                    return (false, 0, "A transaction error occurred during handover. No changes were committed.");
                }
            });
        }

        public async Task<List<HandoverRecordViewModel>> GetHandoverHistoryAsync()
        {
            var handovers = await _context.Handovers
                .Include(h => h.Item)
                .Include(h => h.ReceivingUser)
                .Include(h => h.SecurityUser)
                .OrderByDescending(h => h.HandoverDateTime)
                .ToListAsync();

            return handovers.Select(h => new HandoverRecordViewModel
            {
                HandoverId = h.HandoverId,
                ItemId = h.ItemId,
                ItemTitle = h.Item?.Title ?? "Item",
                ReceivingUserName = h.ReceivingUser?.FullName ?? "Claimant",
                ReceivingUserRegNo = h.ReceivingUser?.RegistrationNumber ?? "N/A",
                SecurityOfficerName = h.SecurityUser?.FullName ?? "Security Officer",
                HandoverDateTime = h.HandoverDateTime,
                ProofReference = h.ProofReference,
                Notes = h.Notes
            }).ToList();
        }

        public async Task<(bool Success, int DisposalId, string? ErrorMessage)> RecordDisposalAsync(DisposalCreateViewModel model, string authorizedUserId, string? userEmail)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<(bool Success, int DisposalId, string? ErrorMessage)>(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var item = await _context.Items.FindAsync(model.ItemId);
                    if (item == null)
                    {
                        return (false, 0, "Item not found.");
                    }

                    if (item.Status == ItemStatuses.Returned)
                    {
                        return (false, 0, "Cannot dispose an item that has already been returned to its owner.");
                    }

                    if (item.Status == ItemStatuses.Disposed)
                    {
                        return (false, 0, "Item has already been marked as disposed.");
                    }

                    var disposal = new DisposalRecord
                    {
                        ItemId = model.ItemId,
                        DisposalReason = model.DisposalReason.Trim(),
                        AuthorizedBy = authorizedUserId,
                        DisposalDate = DateTime.UtcNow,
                        Notes = model.Notes?.Trim()
                    };

                    await _context.DisposalRecords.AddAsync(disposal);

                    // Update item status
                    item.Status = ItemStatuses.Disposed;
                    item.UpdatedAt = DateTime.UtcNow;

                    // Release custody
                    var activeCustodies = await _context.CustodyRecords
                        .Where(c => c.ItemId == model.ItemId && c.ReleasedAt == null)
                        .ToListAsync();

                    foreach (var custody in activeCustodies)
                    {
                        custody.ReleasedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await _auditService.LogActionAsync(
                        authorizedUserId,
                        userEmail,
                        "ItemDisposed",
                        "DisposalRecord",
                        disposal.DisposalId.ToString(),
                        $"Item #{item.ItemId} ('{item.Title}') authorized for disposal. Reason: {disposal.DisposalReason}");

                    return (true, disposal.DisposalId, null);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed while recording disposal for item {ItemId}", model.ItemId);
                    return (false, 0, "A database error occurred while recording disposal.");
                }
            });
        }
    }
}
