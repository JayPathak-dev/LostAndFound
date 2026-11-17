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
    public class ClaimService : IClaimService
    {
        private readonly ApplicationDbContext _context;
        private readonly IVerificationService _verificationService;
        private readonly IAuditService _auditService;
        private readonly ILogger<ClaimService> _logger;

        public ClaimService(
            ApplicationDbContext context,
            IVerificationService verificationService,
            IAuditService auditService,
            ILogger<ClaimService> logger)
        {
            _context = context;
            _verificationService = verificationService;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<(bool Success, int ClaimId, string? ErrorMessage)> SubmitClaimAsync(ClaimCreateViewModel model, string claimantId, string? claimantEmail)
        {
            try
            {
                var item = await _context.Items
                    .Include(i => i.PrivateDetail)
                    .FirstOrDefaultAsync(i => i.ItemId == model.ItemId);

                if (item == null)
                {
                    return (false, 0, "The requested item does not exist.");
                }

                if (item.Status == ItemStatuses.Returned || item.Status == ItemStatuses.Disposed)
                {
                    return (false, 0, $"Claims cannot be submitted because this item is already {item.Status}.");
                }

                if (item.ReporterId == claimantId)
                {
                    return (false, 0, "You cannot submit an ownership claim on an item that you reported.");
                }

                // Check for existing active claim by the same user
                bool hasDuplicate = await _context.Claims.AnyAsync(c =>
                    c.ItemId == model.ItemId &&
                    c.ClaimantId == claimantId &&
                    c.Status != ClaimStatuses.Rejected);

                if (hasDuplicate)
                {
                    return (false, 0, "You have already submitted an active ownership claim for this item.");
                }

                var claim = new Claim
                {
                    ItemId = model.ItemId,
                    ClaimantId = claimantId,
                    ClaimDescription = model.ClaimDescription.Trim(),
                    Status = ClaimStatuses.Pending,
                    SubmittedAt = DateTime.UtcNow
                };

                await _context.Claims.AddAsync(claim);
                await _context.SaveChangesAsync();

                // If challenges were answered in the form, process them
                if (model.ChallengeAnswers != null && model.ChallengeAnswers.Any())
                {
                    await _verificationService.SubmitVerificationAnswersAsync(claim.ClaimId, model.ChallengeAnswers, claimantId);
                    claim.Status = ClaimStatuses.UnderVerification;
                }
                else if (item.PrivateDetail != null)
                {
                    // Generate challenges for claimant to answer
                    await _verificationService.GenerateChallengesForClaimAsync(claim.ClaimId);
                }

                // Update item status to Claimed or UnderVerification if currently Active
                if (item.Status == ItemStatuses.Active || item.Status == ItemStatuses.Reported)
                {
                    item.Status = claim.Status == ClaimStatuses.UnderVerification ? ItemStatuses.UnderVerification : ItemStatuses.Claimed;
                }

                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(
                    claimantId,
                    claimantEmail,
                    "ClaimSubmitted",
                    "Claim",
                    claim.ClaimId.ToString(),
                    $"Submitted claim for item #{item.ItemId} ('{item.Title}').");

                return (true, claim.ClaimId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit claim on item {ItemId} for user {ClaimantId}", model.ItemId, claimantId);
                return (false, 0, "An error occurred while saving your claim. Please try again.");
            }
        }

        public async Task<List<ClaimSummaryViewModel>> GetUserClaimsAsync(string claimantId)
        {
            return await _context.Claims
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Category)
                .Where(c => c.ClaimantId == claimantId)
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new ClaimSummaryViewModel
                {
                    ClaimId = c.ClaimId,
                    ItemId = c.ItemId,
                    ItemTitle = c.Item != null ? c.Item.Title : "Item",
                    CategoryName = c.Item != null && c.Item.Category != null ? c.Item.Category.CategoryName : "General",
                    Status = c.Status,
                    SubmittedAt = c.SubmittedAt,
                    ReviewNotes = c.ReviewNotes
                })
                .ToListAsync();
        }

        public async Task<List<ClaimDetailsViewModel>> GetPendingClaimsAsync()
        {
            var claims = await _context.Claims
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Category)
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Location)
                .Include(c => c.Claimant)
                .Where(c => c.Status == ClaimStatuses.Pending || c.Status == ClaimStatuses.UnderVerification || c.Status == ClaimStatuses.ManualReview)
                .OrderBy(c => c.SubmittedAt)
                .ToListAsync();

            return claims.Select(MapToDetailsViewModel).ToList();
        }

        public async Task<List<ClaimDetailsViewModel>> GetApprovedClaimsForHandoverAsync()
        {
            var claims = await _context.Claims
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Category)
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Location)
                .Include(c => c.Claimant)
                .Include(c => c.Handover)
                .Where(c => c.Status == ClaimStatuses.Approved && c.Handover == null)
                .OrderBy(c => c.ReviewedAt)
                .ToListAsync();

            return claims.Select(MapToDetailsViewModel).ToList();
        }

        public async Task<ClaimDetailsViewModel?> GetClaimDetailsAsync(int claimId, string currentUserId, bool isPrivileged)
        {
            var claim = await _context.Claims
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Category)
                .Include(c => c.Item)
                    .ThenInclude(i => i!.Location)
                .Include(c => c.Item)
                    .ThenInclude(i => i!.PrivateDetail)
                .Include(c => c.Claimant)
                .Include(c => c.Reviewer)
                .Include(c => c.Handover)
                    .ThenInclude(h => h!.SecurityUser)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            if (claim == null) return null;

            // Only claimant, security personnel, or admin can access
            if (!isPrivileged && claim.ClaimantId != currentUserId)
            {
                return null;
            }

            var model = MapToDetailsViewModel(claim);
            model.CanReview = isPrivileged && (claim.Status == ClaimStatuses.Pending || claim.Status == ClaimStatuses.UnderVerification || claim.Status == ClaimStatuses.ManualReview);
            model.CanInitiateHandover = isPrivileged && claim.Status == ClaimStatuses.Approved && claim.Handover == null;
            model.HandoverCompleted = claim.Handover != null;

            if (claim.Handover != null)
            {
                model.HandoverDetails = new HandoverSummaryViewModel
                {
                    HandoverId = claim.Handover.HandoverId,
                    SecurityOfficerName = claim.Handover.SecurityUser?.FullName ?? "Security Officer",
                    HandoverDateTime = claim.Handover.HandoverDateTime,
                    ProofReference = claim.Handover.ProofReference,
                    Notes = claim.Handover.Notes
                };
            }

            // Load verification challenge results with answer comparison
            model.Challenges = await _verificationService.GetVerificationResultsForClaimAsync(claimId, isPrivileged);

            return model;
        }

        public async Task<(bool Success, string? ErrorMessage)> ReviewClaimAsync(ClaimReviewViewModel model, string reviewerId, string? reviewerEmail)
        {
            var claim = await _context.Claims
                .Include(c => c.Item)
                .FirstOrDefaultAsync(c => c.ClaimId == model.ClaimId);

            if (claim == null)
            {
                return (false, "Claim not found.");
            }

            if (!ClaimStatuses.All.Contains(model.Status))
            {
                return (false, "Invalid decision status provided.");
            }

            claim.Status = model.Status;
            claim.ReviewedAt = DateTime.UtcNow;
            claim.ReviewedBy = reviewerId;
            claim.ReviewNotes = model.ReviewNotes?.Trim();

            // Update item status based on claim decision
            if (claim.Item != null)
            {
                if (model.Status == ClaimStatuses.Approved)
                {
                    claim.Item.Status = ItemStatuses.Approved;
                    // Reject competing pending claims for the same item
                    var otherClaims = await _context.Claims
                        .Where(c => c.ItemId == claim.ItemId && c.ClaimId != claim.ClaimId && c.Status != ClaimStatuses.Rejected)
                        .ToListAsync();

                    foreach (var other in otherClaims)
                    {
                        other.Status = ClaimStatuses.Rejected;
                        other.ReviewedAt = DateTime.UtcNow;
                        other.ReviewedBy = reviewerId;
                        other.ReviewNotes = "Another claimant provided verified proof of ownership.";
                    }
                }
                else if (model.Status == ClaimStatuses.Rejected)
                {
                    // Check if other active claims exist for this item
                    bool otherActiveClaims = await _context.Claims.AnyAsync(c =>
                        c.ItemId == claim.ItemId && c.ClaimId != claim.ClaimId && c.Status != ClaimStatuses.Rejected);

                    if (!otherActiveClaims)
                    {
                        claim.Item.Status = ItemStatuses.Active; // Return item to active state
                    }
                }
                else if (model.Status == ClaimStatuses.ManualReview)
                {
                    claim.Item.Status = ItemStatuses.UnderVerification;
                }
            }

            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                reviewerId,
                reviewerEmail,
                $"Claim{model.Status}",
                "Claim",
                claim.ClaimId.ToString(),
                $"Claim decision: {model.Status}. Item #{claim.ItemId}. Notes: {model.ReviewNotes}");

            return (true, null);
        }

        private static ClaimDetailsViewModel MapToDetailsViewModel(Claim claim)
        {
            return new ClaimDetailsViewModel
            {
                ClaimId = claim.ClaimId,
                ItemId = claim.ItemId,
                ItemTitle = claim.Item?.Title ?? "Item",
                ItemCategory = claim.Item?.Category?.CategoryName ?? "General",
                ItemLocation = claim.Item?.Location?.DisplayName ?? "Campus",
                ItemImagePath = claim.Item?.ImagePath,
                ItemStatus = claim.Item?.Status ?? "Unknown",
                ClaimantId = claim.ClaimantId,
                ClaimantName = claim.Claimant?.FullName ?? "Claimant",
                ClaimantEmail = claim.Claimant?.Email ?? string.Empty,
                ClaimantRegNo = claim.Claimant?.RegistrationNumber,
                ClaimantPhone = claim.Claimant?.PhoneNumber,
                ClaimDescription = claim.ClaimDescription,
                Status = claim.Status,
                SubmittedAt = claim.SubmittedAt,
                ReviewedAt = claim.ReviewedAt,
                ReviewerName = claim.Reviewer?.FullName,
                ReviewNotes = claim.ReviewNotes
            };
        }
    }
}
