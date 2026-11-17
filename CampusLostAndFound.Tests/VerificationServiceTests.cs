using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using CampusLostAndFound.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CampusLostAndFound.Tests
{
    public class VerificationServiceTests
    {
        private readonly ApplicationDbContext _context;
        private readonly VerificationService _verificationService;

        public VerificationServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source=test_verification_{Guid.NewGuid():N}.db")
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            var auditService = new AuditService(_context, NullLogger<AuditService>.Instance);
            _verificationService = new VerificationService(_context, auditService, NullLogger<VerificationService>.Instance);
        }

        private async Task<(ItemCategory Cat, CampusLocation Loc, ApplicationUser Reporter, ApplicationUser Claimant)> SeedPrerequisitesAsync()
        {
            var cat = new ItemCategory { CategoryName = "Bags", RetentionPeriodDays = 60 };
            var loc = new CampusLocation { BuildingName = "Library", AreaName = "Hall" };
            var reporter = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "rep@campus.edu", FullName = "Reporter" };
            var claimant = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "clm@campus.edu", FullName = "Claimant" };

            await _context.ItemCategories.AddAsync(cat);
            await _context.CampusLocations.AddAsync(loc);
            await _context.Users.AddRangeAsync(reporter, claimant);
            await _context.SaveChangesAsync();

            return (cat, loc, reporter, claimant);
        }

        [Fact]
        public async Task GenerateChallenges_CreatesChallengesFromPrivateDetailsWithoutLeakingAnswers()
        {
            // Arrange
            var (cat, loc, reporter, claimant) = await SeedPrerequisitesAsync();

            var item = new Item
            {
                Title = "Leather Backpack",
                Description = "Brown backpack found in library",
                ReportType = ReportTypes.Found,
                Status = ItemStatuses.Active,
                ReporterId = reporter.Id,
                CategoryId = cat.CategoryId,
                LocationId = loc.LocationId,
                ItemDateTime = DateTime.UtcNow
            };
            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var privateDetail = new PrivateItemDetail
            {
                ItemId = item.ItemId,
                DistinctiveMarks = "Small tear near left zipper",
                InternalContents = "Contains a blue spiral notebook and room key 204"
            };
            var claim = new Claim
            {
                ItemId = item.ItemId,
                ClaimantId = claimant.Id,
                ClaimDescription = "This is my backpack",
                Status = ClaimStatuses.Pending,
                SubmittedAt = DateTime.UtcNow
            };

            await _context.PrivateItemDetails.AddAsync(privateDetail);
            await _context.Claims.AddAsync(claim);
            await _context.SaveChangesAsync();

            // Act
            var challenges = await _verificationService.GenerateChallengesForClaimAsync(claim.ClaimId);

            // Assert
            Assert.Equal(2, challenges.Count);
            Assert.Contains(challenges, c => c.ChallengeType == "DistinctiveMarks");
            Assert.Contains(challenges, c => c.ChallengeType == "InternalContents");

            // Verify that expected answers are NOT contained in the public prompt
            foreach (var ch in challenges)
            {
                Assert.DoesNotContain("blue spiral notebook", ch.QuestionPrompt, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("left zipper", ch.QuestionPrompt, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task EvaluateClaimVerification_HighSimilarity_RecommendsApproved()
        {
            // Arrange
            var (cat, loc, reporter, claimant) = await SeedPrerequisitesAsync();

            var item = new Item
            {
                Title = "Wallet",
                Description = "Leather wallet",
                ReportType = ReportTypes.Found,
                Status = ItemStatuses.Active,
                ReporterId = reporter.Id,
                CategoryId = cat.CategoryId,
                LocationId = loc.LocationId,
                ItemDateTime = DateTime.UtcNow
            };
            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var claim = new Claim
            {
                ItemId = item.ItemId,
                ClaimantId = claimant.Id,
                ClaimDescription = "Lost my wallet",
                Status = ClaimStatuses.UnderVerification,
                SubmittedAt = DateTime.UtcNow
            };
            await _context.Claims.AddAsync(claim);
            await _context.SaveChangesAsync();

            var ch1 = new VerificationChallenge
            {
                ClaimId = claim.ClaimId,
                ChallengeType = "DistinctiveMarks",
                QuestionPrompt = "Any marks?",
                ExpectedAnswerHash = "HASH1",
                SubmittedAnswer = "Initials JD stamped on bottom",
                SimilarityScore = 90.0,
                IsCorrect = true
            };
            var ch2 = new VerificationChallenge
            {
                ClaimId = claim.ClaimId,
                ChallengeType = "InternalContents",
                QuestionPrompt = "Internal items?",
                ExpectedAnswerHash = "HASH2",
                SubmittedAnswer = "Metro card and student id ending in 1042",
                SimilarityScore = 85.0,
                IsCorrect = true
            };

            await _context.VerificationChallenges.AddRangeAsync(ch1, ch2);
            await _context.SaveChangesAsync();

            // Act
            var (passed, averageScore, outcome) = await _verificationService.EvaluateClaimVerificationAsync(claim.ClaimId);

            // Assert
            Assert.True(passed);
            Assert.True(averageScore >= 70.0);
            Assert.Equal(ClaimStatuses.Approved, outcome);
        }
    }
}
