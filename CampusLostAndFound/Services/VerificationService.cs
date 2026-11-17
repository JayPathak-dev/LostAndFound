using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public class VerificationService : IVerificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ILogger<VerificationService> _logger;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "in", "on", "at", "to", "for", "of", "and", "or", "is", "it",
            "my", "i", "was", "with", "has", "had", "have", "there", "some", "this", "that"
        };

        public VerificationService(ApplicationDbContext context, IAuditService auditService, ILogger<VerificationService> logger)
        {
            _context = context;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<List<ChallengeAnswerInputModel>> GenerateChallengesForClaimAsync(int claimId)
        {
            var claim = await _context.Claims
                .Include(c => c.Item)
                .ThenInclude(i => i!.PrivateDetail)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            if (claim?.Item?.PrivateDetail == null)
            {
                return new List<ChallengeAnswerInputModel>();
            }

            // Check if challenges already exist
            var existingChallenges = await _context.VerificationChallenges
                .Where(v => v.ClaimId == claimId)
                .ToListAsync();

            if (!existingChallenges.Any())
            {
                var privateDetail = claim.Item.PrivateDetail;
                var challengesToCreate = new List<VerificationChallenge>();

                if (!string.IsNullOrWhiteSpace(privateDetail.DistinctiveMarks))
                {
                    challengesToCreate.Add(new VerificationChallenge
                    {
                        ClaimId = claimId,
                        ChallengeType = "DistinctiveMarks",
                        QuestionPrompt = "Are there any specific distinctive marks, scratches, or visual identifiers on this item? Please describe in detail.",
                        ExpectedAnswerHash = ComputeNormalizedHash(privateDetail.DistinctiveMarks),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!string.IsNullOrWhiteSpace(privateDetail.HiddenFeatures))
                {
                    challengesToCreate.Add(new VerificationChallenge
                    {
                        ClaimId = claimId,
                        ChallengeType = "HiddenFeatures",
                        QuestionPrompt = "Does this item possess any concealed features, hidden compartments, or special characteristics?",
                        ExpectedAnswerHash = ComputeNormalizedHash(privateDetail.HiddenFeatures),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!string.IsNullOrWhiteSpace(privateDetail.InternalContents))
                {
                    challengesToCreate.Add(new VerificationChallenge
                    {
                        ClaimId = claimId,
                        ChallengeType = "InternalContents",
                        QuestionPrompt = "If this item contains internal contents (e.g. inside a wallet, bag, or case), specify the exact belongings stored inside.",
                        ExpectedAnswerHash = ComputeNormalizedHash(privateDetail.InternalContents),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!string.IsNullOrWhiteSpace(privateDetail.AdditionalIdentifiers))
                {
                    challengesToCreate.Add(new VerificationChallenge
                    {
                        ClaimId = claimId,
                        ChallengeType = "AdditionalIdentifiers",
                        QuestionPrompt = "Provide any specific serial number fragment, sticker, tag, or attachment unique to your belonging.",
                        ExpectedAnswerHash = ComputeNormalizedHash(privateDetail.AdditionalIdentifiers),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (challengesToCreate.Any())
                {
                    await _context.VerificationChallenges.AddRangeAsync(challengesToCreate);
                    await _context.SaveChangesAsync();
                    existingChallenges = challengesToCreate;
                }
            }

            // Return safe question models WITHOUT expected answers
            return existingChallenges.Select(c => new ChallengeAnswerInputModel
            {
                ChallengeId = c.ChallengeId,
                ChallengeType = c.ChallengeType,
                QuestionPrompt = c.QuestionPrompt,
                Answer = c.SubmittedAnswer ?? string.Empty
            }).ToList();
        }

        public async Task<bool> SubmitVerificationAnswersAsync(int claimId, List<ChallengeAnswerInputModel> answers, string claimantUserId)
        {
            var claim = await _context.Claims
                .Include(c => c.Item)
                .ThenInclude(i => i!.PrivateDetail)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            if (claim == null || claim.ClaimantId != claimantUserId)
            {
                return false;
            }

            var challenges = await _context.VerificationChallenges
                .Where(v => v.ClaimId == claimId)
                .ToListAsync();

            var privateDetail = claim.Item?.PrivateDetail;

            foreach (var input in answers)
            {
                var challenge = challenges.FirstOrDefault(c => c.ChallengeId == input.ChallengeId);
                if (challenge != null && !string.IsNullOrWhiteSpace(input.Answer))
                {
                    challenge.SubmittedAnswer = input.Answer.Trim();
                    challenge.AnsweredAt = DateTime.UtcNow;

                    // Compute similarity against corresponding private detail field
                    string expectedFieldText = challenge.ChallengeType switch
                    {
                        "DistinctiveMarks" => privateDetail?.DistinctiveMarks ?? string.Empty,
                        "HiddenFeatures" => privateDetail?.HiddenFeatures ?? string.Empty,
                        "InternalContents" => privateDetail?.InternalContents ?? string.Empty,
                        "AdditionalIdentifiers" => privateDetail?.AdditionalIdentifiers ?? string.Empty,
                        _ => string.Empty
                    };

                    double score = CalculateAnswerSimilarity(input.Answer, expectedFieldText);
                    challenge.SimilarityScore = Math.Round(score * 100, 1);
                    challenge.IsCorrect = score >= 0.40;
                }
            }

            claim.Status = ClaimStatuses.UnderVerification;
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                claimantUserId,
                null,
                "VerificationSubmitted",
                "Claim",
                claimId.ToString(),
                $"Submitted responses for {answers.Count} verification challenges.");

            return true;
        }

        public async Task<List<VerificationChallengeResultViewModel>> GetVerificationResultsForClaimAsync(int claimId, bool isPrivilegedReviewer)
        {
            var challenges = await _context.VerificationChallenges
                .Include(v => v.Claim)
                .ThenInclude(c => c!.Item)
                .ThenInclude(i => i!.PrivateDetail)
                .Where(v => v.ClaimId == claimId)
                .ToListAsync();

            return challenges.Select(c =>
            {
                string? expected = null;
                if (isPrivilegedReviewer && c.Claim?.Item?.PrivateDetail != null)
                {
                    expected = c.ChallengeType switch
                    {
                        "DistinctiveMarks" => c.Claim.Item.PrivateDetail.DistinctiveMarks,
                        "HiddenFeatures" => c.Claim.Item.PrivateDetail.HiddenFeatures,
                        "InternalContents" => c.Claim.Item.PrivateDetail.InternalContents,
                        "AdditionalIdentifiers" => c.Claim.Item.PrivateDetail.AdditionalIdentifiers,
                        _ => null
                    };
                }

                return new VerificationChallengeResultViewModel
                {
                    ChallengeId = c.ChallengeId,
                    ChallengeType = c.ChallengeType,
                    QuestionPrompt = c.QuestionPrompt,
                    SubmittedAnswer = c.SubmittedAnswer,
                    IsCorrect = c.IsCorrect,
                    SimilarityScore = c.SimilarityScore,
                    ExpectedReference = expected // Only populated for authorized reviewers!
                };
            }).ToList();
        }

        public async Task<(bool Passed, double AverageScore, string SuggestedOutcome)> EvaluateClaimVerificationAsync(int claimId)
        {
            var challenges = await _context.VerificationChallenges
                .Where(v => v.ClaimId == claimId)
                .ToListAsync();

            if (!challenges.Any())
            {
                return (false, 0.0, ClaimStatuses.ManualReview);
            }

            double totalScore = challenges.Sum(c => c.SimilarityScore ?? 0.0);
            double average = totalScore / challenges.Count;

            string suggestedOutcome;
            bool passed = false;

            if (average >= 70.0)
            {
                suggestedOutcome = ClaimStatuses.Approved;
                passed = true;
            }
            else if (average >= 40.0)
            {
                suggestedOutcome = ClaimStatuses.ManualReview;
            }
            else
            {
                suggestedOutcome = ClaimStatuses.Rejected;
            }

            return (passed, Math.Round(average, 1), suggestedOutcome);
        }

        private static string ComputeNormalizedHash(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string normalized = Regex.Replace(input.ToLowerInvariant(), @"[^\w\s]", "")
                .Trim();

            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes);
        }

        private static double CalculateAnswerSimilarity(string submitted, string expected)
        {
            if (string.IsNullOrWhiteSpace(submitted) || string.IsNullOrWhiteSpace(expected)) return 0.0;

            var submittedTokens = Tokenize(submitted);
            var expectedTokens = Tokenize(expected);

            if (!expectedTokens.Any() || !submittedTokens.Any()) return 0.0;

            // Check exact key token matches
            int matchingTokens = submittedTokens.Intersect(expectedTokens, StringComparer.OrdinalIgnoreCase).Count();
            double tokenRecall = (double)matchingTokens / expectedTokens.Count;

            // Also check for numeric fragments or exact substring containment
            bool directContainment = expected.ToLowerInvariant().Contains(submitted.ToLowerInvariant()) ||
                                     submitted.ToLowerInvariant().Contains(expected.ToLowerInvariant());

            if (directContainment)
            {
                return Math.Max(tokenRecall, 0.85);
            }

            return tokenRecall;
        }

        private static HashSet<string> Tokenize(string text)
        {
            string clean = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", " ");
            return clean.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 2 && !StopWords.Contains(w))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
