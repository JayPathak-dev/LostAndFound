using System;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CampusLostAndFound.Tests
{
    public class MatchingEngineTests
    {
        private readonly MatchingService _matchingService;

        public MatchingEngineTests()
        {
            var options = new DbContextOptionsBuilder<Data.ApplicationDbContext>()
                .UseSqlite("Data Source=test_matching.db")
                .Options;

            var context = new Data.ApplicationDbContext(options);
            context.Database.EnsureCreated();
            _matchingService = new MatchingService(context, NullLogger<MatchingService>.Instance);
        }

        [Fact]
        public void CalculateMatchScore_IdenticalItems_ReturnsHighMatchScore()
        {
            // Arrange
            var category = new ItemCategory { CategoryId = 1, CategoryName = "Electronics" };
            var location = new CampusLocation { LocationId = 1, BuildingName = "Central Library", AreaName = "Reading Hall" };

            var lostItem = new Item
            {
                ItemId = 1,
                Title = "Dell 65W USB-C Charger",
                Description = "Misplaced my black Dell laptop charger under table",
                ReportType = ReportTypes.Lost,
                CategoryId = 1,
                Category = category,
                LocationId = 1,
                Location = location,
                ItemDateTime = DateTime.UtcNow
            };

            var foundItem = new Item
            {
                ItemId = 2,
                Title = "Dell 65W USB-C Charger",
                Description = "Found black Dell fast charger plugged into socket under table",
                ReportType = ReportTypes.Found,
                CategoryId = 1,
                Category = category,
                LocationId = 1,
                Location = location,
                ItemDateTime = DateTime.UtcNow.AddHours(2)
            };

            // Act
            var (score, reason) = _matchingService.CalculateMatchScore(lostItem, foundItem);

            // Assert
            Assert.True(score >= 80.0, $"Expected score >= 80, but got {score}. Reason: {reason}");
            Assert.Contains("category match", reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("location match", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CalculateMatchScore_CompletelyDifferentItems_ReturnsLowScore()
        {
            // Arrange
            var cat1 = new ItemCategory { CategoryId = 1, CategoryName = "Electronics" };
            var cat2 = new ItemCategory { CategoryId = 2, CategoryName = "Clothing" };
            var loc1 = new CampusLocation { LocationId = 1, BuildingName = "Central Library", AreaName = "Reading Hall" };
            var loc2 = new CampusLocation { LocationId = 2, BuildingName = "Sports Complex", AreaName = "Badminton Court" };

            var lostItem = new Item
            {
                ItemId = 1,
                Title = "Dell Laptop Charger",
                Description = "Black Dell power cord",
                ReportType = ReportTypes.Lost,
                CategoryId = 1,
                Category = cat1,
                LocationId = 1,
                Location = loc1,
                ItemDateTime = DateTime.UtcNow.AddDays(-20)
            };

            var foundItem = new Item
            {
                ItemId = 2,
                Title = "Red Woolen Winter Jacket",
                Description = "Adidas red jacket left on bleachers",
                ReportType = ReportTypes.Found,
                CategoryId = 2,
                Category = cat2,
                LocationId = 2,
                Location = loc2,
                ItemDateTime = DateTime.UtcNow
            };

            // Act
            var (score, reason) = _matchingService.CalculateMatchScore(lostItem, foundItem);

            // Assert
            Assert.True(score < 30.0, $"Expected score < 30, but got {score}. Reason: {reason}");
        }

        [Fact]
        public void CalculateMatchScore_SameBuildingDifferentArea_AwardsPartialLocationScore()
        {
            // Arrange
            var category = new ItemCategory { CategoryId = 1, CategoryName = "Books" };
            var loc1 = new CampusLocation { LocationId = 1, BuildingName = "Academic Block A", AreaName = "Room 101" };
            var loc2 = new CampusLocation { LocationId = 2, BuildingName = "Academic Block A", AreaName = "Room 304" };

            var lost = new Item
            {
                ItemId = 1,
                Title = "Algorithms Textbook Cormen",
                Description = "Introduction to Algorithms CLRS edition",
                ReportType = ReportTypes.Lost,
                CategoryId = 1,
                Category = category,
                LocationId = 1,
                Location = loc1,
                ItemDateTime = DateTime.UtcNow
            };

            var found = new Item
            {
                ItemId = 2,
                Title = "Algorithms Textbook",
                Description = "CLRS book left on bench",
                ReportType = ReportTypes.Found,
                CategoryId = 1,
                Category = category,
                LocationId = 2,
                Location = loc2,
                ItemDateTime = DateTime.UtcNow.AddHours(5)
            };

            // Act
            var (score, reason) = _matchingService.CalculateMatchScore(lost, found);

            // Assert
            Assert.Contains("Same building (Academic Block A)", reason);
            Assert.True(score >= 50.0);
        }
    }
}
