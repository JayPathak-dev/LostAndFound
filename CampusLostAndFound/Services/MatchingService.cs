using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public class MatchingService : IMatchingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MatchingService> _logger;

        // Configurable weights (Total = 100%)
        private const double WeightTitleKeywords = 35.0;
        private const double WeightCategory = 20.0;
        private const double WeightLocation = 20.0;
        private const double WeightDateTime = 15.0;
        private const double WeightDescription = 10.0;

        private const double MinimumSuggestionThreshold = 45.0;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "in", "on", "at", "to", "for", "of", "and", "or", "is", "it",
            "my", "i", "was", "with", "lost", "found", "near", "by", "some", "this", "that",
            "has", "had", "have", "left", "behind", "misplaced", "dropped"
        };

        public MatchingService(ApplicationDbContext context, ILogger<MatchingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public (double Score, string Reason) CalculateMatchScore(Item lostItem, Item foundItem)
        {
            if (lostItem == null || foundItem == null) return (0, "Invalid items");

            var reasons = new List<string>();
            double totalScore = 0.0;

            // 1. Category Match (20%)
            if (lostItem.CategoryId == foundItem.CategoryId)
            {
                totalScore += WeightCategory;
                reasons.Add($"Exact category match ({lostItem.Category?.CategoryName ?? "Category"}) (+{WeightCategory:0}%)");
            }

            // 2. Location Match (20%)
            if (lostItem.LocationId == foundItem.LocationId)
            {
                totalScore += WeightLocation;
                reasons.Add($"Exact campus location match ({lostItem.Location?.DisplayName ?? "Location"}) (+{WeightLocation:0}%)");
            }
            else if (lostItem.Location != null && foundItem.Location != null &&
                     string.Equals(lostItem.Location.BuildingName, foundItem.Location.BuildingName, StringComparison.OrdinalIgnoreCase))
            {
                double partialLocation = WeightLocation * 0.6; // 12%
                totalScore += partialLocation;
                reasons.Add($"Same building ({lostItem.Location.BuildingName}) (+{partialLocation:0}%)");
            }

            // 3. Date & Time Proximity (15%)
            double hoursDiff = Math.Abs((lostItem.ItemDateTime - foundItem.ItemDateTime).TotalHours);
            if (hoursDiff <= 24)
            {
                totalScore += WeightDateTime;
                reasons.Add($"Time proximity within 24 hours (+{WeightDateTime:0}%)");
            }
            else if (hoursDiff <= 72)
            {
                double partialTime = WeightDateTime * 0.7; // 10.5%
                totalScore += partialTime;
                reasons.Add($"Time proximity within 3 days (+{partialTime:0.0}%)");
            }
            else if (hoursDiff <= 168)
            {
                double partialTime = WeightDateTime * 0.4; // 6%
                totalScore += partialTime;
                reasons.Add($"Time proximity within 7 days (+{partialTime:0.0}%)");
            }
            else if (hoursDiff <= 336)
            {
                double partialTime = WeightDateTime * 0.2; // 3%
                totalScore += partialTime;
                reasons.Add($"Time proximity within 14 days (+{partialTime:0.0}%)");
            }

            // 4. Title Keywords Similarity (35%)
            var lostTitleTokens = Tokenize(lostItem.Title);
            var foundTitleTokens = Tokenize(foundItem.Title);

            double titleSimilarity = CalculateTokenSimilarity(lostTitleTokens, foundTitleTokens);
            double titleScore = titleSimilarity * WeightTitleKeywords;
            totalScore += titleScore;

            var matchingTitleWords = lostTitleTokens.Intersect(foundTitleTokens, StringComparer.OrdinalIgnoreCase).ToList();
            if (matchingTitleWords.Any())
            {
                reasons.Add($"Matched title terms [{string.Join(", ", matchingTitleWords)}] (+{titleScore:0.0}%)");
            }

            // 5. Description Similarity (10%)
            var lostDescTokens = Tokenize(lostItem.Description);
            var foundDescTokens = Tokenize(foundItem.Description);

            double descSimilarity = CalculateTokenSimilarity(lostDescTokens, foundDescTokens);
            double descScore = descSimilarity * WeightDescription;
            totalScore += descScore;

            var matchingDescWords = lostDescTokens.Intersect(foundDescTokens, StringComparer.OrdinalIgnoreCase).Take(4).ToList();
            if (matchingDescWords.Any())
            {
                reasons.Add($"Common description keywords [{string.Join(", ", matchingDescWords)}] (+{descScore:0.0}%)");
            }

            totalScore = Math.Min(100.0, Math.Round(totalScore, 1));
            string overallReason = string.Join("; ", reasons);

            return (totalScore, overallReason);
        }

        public async Task<int> FindAndSaveMatchesForReportAsync(int itemId)
        {
            var item = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .FirstOrDefaultAsync(i => i.ItemId == itemId);

            if (item == null) return 0;

            int newMatchesFound = 0;
            string oppositeType = item.ReportType == ReportTypes.Lost ? ReportTypes.Found : ReportTypes.Lost;

            var candidates = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Location)
                .Where(c => c.ReportType == oppositeType &&
                            (c.Status == ItemStatuses.Active || c.Status == ItemStatuses.Reported) &&
                            c.ItemId != itemId)
                .ToListAsync();

            foreach (var candidate in candidates)
            {
                Item lostItem = item.ReportType == ReportTypes.Lost ? item : candidate;
                Item foundItem = item.ReportType == ReportTypes.Found ? item : candidate;

                var (score, reason) = CalculateMatchScore(lostItem, foundItem);

                if (score >= MinimumSuggestionThreshold)
                {
                    var existing = await _context.MatchSuggestions
                        .FirstOrDefaultAsync(m => m.LostItemId == lostItem.ItemId && m.FoundItemId == foundItem.ItemId);

                    if (existing == null)
                    {
                        var suggestion = new MatchSuggestion
                        {
                            LostItemId = lostItem.ItemId,
                            FoundItemId = foundItem.ItemId,
                            MatchScore = score,
                            MatchReason = reason,
                            ReviewStatus = MatchReviewStatuses.New,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MatchSuggestions.Add(suggestion);
                        newMatchesFound++;
                    }
                    else
                    {
                        existing.MatchScore = score;
                        existing.MatchReason = reason;
                    }
                }
            }

            if (newMatchesFound > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved {Count} match suggestions for item {ItemId}", newMatchesFound, itemId);
            }

            return newMatchesFound;
        }

        public async Task<List<MatchSuggestionCardViewModel>> GetMatchesForUserAsync(string userId)
        {
            var userItemIds = await _context.Items
                .Where(i => i.ReporterId == userId)
                .Select(i => i.ItemId)
                .ToListAsync();

            var suggestions = await _context.MatchSuggestions
                .Include(m => m.LostItem)!.ThenInclude(i => i!.Category)
                .Include(m => m.LostItem)!.ThenInclude(i => i!.Location)
                .Include(m => m.FoundItem)!.ThenInclude(i => i!.Category)
                .Include(m => m.FoundItem)!.ThenInclude(i => i!.Location)
                .Where(m => userItemIds.Contains(m.LostItemId) || userItemIds.Contains(m.FoundItemId))
                .OrderByDescending(m => m.MatchScore)
                .ToListAsync();

            return suggestions.Select(m => new MatchSuggestionCardViewModel
            {
                MatchId = m.MatchId,
                LostItemId = m.LostItemId,
                LostItemTitle = m.LostItem?.Title ?? "Lost Item",
                FoundItemId = m.FoundItemId,
                FoundItemTitle = m.FoundItem?.Title ?? "Found Item",
                CategoryName = m.FoundItem?.Category?.CategoryName ?? m.LostItem?.Category?.CategoryName ?? "General",
                LocationName = m.FoundItem?.Location?.DisplayName ?? "Campus",
                MatchScore = m.MatchScore,
                MatchReason = m.MatchReason,
                CreatedAt = m.CreatedAt,
                ReviewStatus = m.ReviewStatus
            }).ToList();
        }

        public async Task<List<MatchSuggestionCardViewModel>> GetMatchesForItemAsync(int itemId)
        {
            var suggestions = await _context.MatchSuggestions
                .Include(m => m.LostItem)!.ThenInclude(i => i!.Category)
                .Include(m => m.LostItem)!.ThenInclude(i => i!.Location)
                .Include(m => m.FoundItem)!.ThenInclude(i => i!.Category)
                .Include(m => m.FoundItem)!.ThenInclude(i => i!.Location)
                .Where(m => m.LostItemId == itemId || m.FoundItemId == itemId)
                .OrderByDescending(m => m.MatchScore)
                .ToListAsync();

            return suggestions.Select(m => new MatchSuggestionCardViewModel
            {
                MatchId = m.MatchId,
                LostItemId = m.LostItemId,
                LostItemTitle = m.LostItem?.Title ?? "Lost Item",
                FoundItemId = m.FoundItemId,
                FoundItemTitle = m.FoundItem?.Title ?? "Found Item",
                CategoryName = m.FoundItem?.Category?.CategoryName ?? "General",
                LocationName = m.FoundItem?.Location?.DisplayName ?? "Campus",
                MatchScore = m.MatchScore,
                MatchReason = m.MatchReason,
                CreatedAt = m.CreatedAt,
                ReviewStatus = m.ReviewStatus
            }).ToList();
        }

        private static HashSet<string> Tokenize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new HashSet<string>();

            // Remove special characters and split on whitespace
            string clean = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s-]", " ");
            var words = clean.Split(new[] { ' ', '\t', '\r', '\n', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            return words
                .Where(w => w.Length >= 2 && !StopWords.Contains(w))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static double CalculateTokenSimilarity(HashSet<string> setA, HashSet<string> setB)
        {
            if (!setA.Any() || !setB.Any()) return 0.0;

            int intersection = setA.Intersect(setB, StringComparer.OrdinalIgnoreCase).Count();
            int union = setA.Union(setB, StringComparer.OrdinalIgnoreCase).Count();

            return union == 0 ? 0.0 : (double)intersection / union;
        }
    }
}
