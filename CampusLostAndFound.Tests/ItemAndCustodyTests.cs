using System;
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
    public class ItemAndCustodyTests
    {
        private readonly ApplicationDbContext _context;
        private readonly ItemService _itemService;
        private readonly ClaimService _claimService;
        private readonly CustodyService _custodyService;

        public ItemAndCustodyTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source=test_custody_{Guid.NewGuid():N}.db")
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            var auditService = new AuditService(_context, NullLogger<AuditService>.Instance);
            var matchingService = new MatchingService(_context, NullLogger<MatchingService>.Instance);
            var fileUploadService = new FileUploadService(null!, NullLogger<FileUploadService>.Instance);
            var verificationService = new VerificationService(_context, auditService, NullLogger<VerificationService>.Instance);

            _itemService = new ItemService(_context, fileUploadService, matchingService, auditService, NullLogger<ItemService>.Instance);
            _claimService = new ClaimService(_context, verificationService, auditService, NullLogger<ClaimService>.Instance);
            _custodyService = new CustodyService(_context, auditService, NullLogger<CustodyService>.Instance);
        }

        private async Task<(ItemCategory Cat, CampusLocation Loc, ApplicationUser User)> SeedPrerequisitesAsync()
        {
            var cat = new ItemCategory { CategoryName = "Electronics", RetentionPeriodDays = 90 };
            var loc = new CampusLocation { BuildingName = "Library", AreaName = "Hall" };
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "user@campus.edu", FullName = "Test User" };

            await _context.ItemCategories.AddAsync(cat);
            await _context.CampusLocations.AddAsync(loc);
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return (cat, loc, user);
        }

        [Fact]
        public async Task GetPublicFoundItems_NeverExposesPrivateDetails()
        {
            // Arrange
            var (cat, loc, user) = await SeedPrerequisitesAsync();

            var item = new Item
            {
                Title = "Found Laptop Charger",
                Description = "Found black charger",
                ReportType = ReportTypes.Found,
                Status = ItemStatuses.Active,
                CategoryId = cat.CategoryId,
                LocationId = loc.LocationId,
                ReporterId = user.Id,
                ItemDateTime = DateTime.UtcNow
            };
            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var privateDetail = new PrivateItemDetail
            {
                ItemId = item.ItemId,
                DistinctiveMarks = "TOP SECRET YELLOW TAPE",
                InternalContents = "SECRET CODE 9988"
            };
            await _context.PrivateItemDetails.AddAsync(privateDetail);
            await _context.SaveChangesAsync();

            // Act
            var searchResult = await _itemService.GetPublicFoundItemsAsync(new ItemSearchViewModel { PageSize = 10 });

            // Assert
            Assert.NotEmpty(searchResult.Items);
            var card = searchResult.Items.Find(i => i.ItemId == item.ItemId);
            Assert.NotNull(card);
            Assert.Equal("Found Laptop Charger", card.Title);

            // ItemCardViewModel has NO properties for DistinctiveMarks, InternalContents, etc.
            var cardType = typeof(ItemCardViewModel);
            Assert.Null(cardType.GetProperty("DistinctiveMarks"));
            Assert.Null(cardType.GetProperty("InternalContents"));
            Assert.Null(cardType.GetProperty("HiddenFeatures"));
            Assert.Null(cardType.GetProperty("PrivateDetail"));
        }

        [Fact]
        public async Task SubmitClaim_ClaimingOwnReportedItem_FailsWithValidationMessage()
        {
            // Arrange
            var (cat, loc, user) = await SeedPrerequisitesAsync();

            var item = new Item
            {
                Title = "Found Keys",
                Description = "Two keys with keychain",
                ReportType = ReportTypes.Found,
                Status = ItemStatuses.Active,
                ReporterId = user.Id,
                CategoryId = cat.CategoryId,
                LocationId = loc.LocationId,
                ItemDateTime = DateTime.UtcNow
            };
            await _context.Items.AddAsync(item);
            await _context.SaveChangesAsync();

            var claimModel = new ClaimCreateViewModel
            {
                ItemId = item.ItemId,
                ItemTitle = "Found Keys",
                ClaimDescription = "These are mine!"
            };

            // Act
            var (success, claimId, error) = await _claimService.SubmitClaimAsync(claimModel, user.Id, user.Email);

            // Assert
            Assert.False(success);
            Assert.Contains("cannot submit an ownership claim on an item that you reported", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CompleteSupervisedHandover_UpdatesItemToReturnedAndReleasesCustody()
        {
            // Arrange
            var (cat, loc, reporter) = await SeedPrerequisitesAsync();
            var claimant = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "claimant@campus.edu", FullName = "Owner User" };
            var secUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "sec@campus.edu", FullName = "Officer John" };

            await _context.Users.AddRangeAsync(claimant, secUser);
            await _context.SaveChangesAsync();

            var item = new Item
            {
                Title = "Dell Laptop Charger",
                Description = "Found Dell charger",
                ReportType = ReportTypes.Found,
                Status = ItemStatuses.Approved,
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
                ClaimDescription = "My charger",
                Status = ClaimStatuses.Approved,
                SubmittedAt = DateTime.UtcNow
            };
            await _context.Claims.AddAsync(claim);
            await _context.SaveChangesAsync();

            var custody = new CustodyRecord
            {
                ItemId = item.ItemId,
                CustodyLocation = "Locker A-1",
                ResponsibleSecurityUserId = secUser.Id,
                ReceivedAt = DateTime.UtcNow.AddDays(-1)
            };
            await _context.CustodyRecords.AddAsync(custody);
            await _context.SaveChangesAsync();

            var handoverModel = new HandoverCreateViewModel
            {
                ClaimId = claim.ClaimId,
                ItemId = item.ItemId,
                ProofReference = "Student ID Card 21BCE1042 Verified",
                Notes = "Physical handover signed"
            };

            // Act
            var (success, handoverId, error) = await _custodyService.CompleteSupervisedHandoverAsync(handoverModel, secUser.Id, secUser.Email);

            // Assert
            Assert.True(success, error);
            Assert.True(handoverId > 0);

            var updatedItem = await _context.Items.FindAsync(item.ItemId);
            Assert.Equal(ItemStatuses.Returned, updatedItem!.Status);

            var updatedCustody = await _context.CustodyRecords.FindAsync(custody.CustodyId);
            Assert.NotNull(updatedCustody!.ReleasedAt);
        }
    }
}
