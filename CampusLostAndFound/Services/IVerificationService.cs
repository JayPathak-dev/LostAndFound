using System.Collections.Generic;
using System.Threading.Tasks;
using CampusLostAndFound.Models;
using CampusLostAndFound.ViewModels;

namespace CampusLostAndFound.Services
{
    public interface IVerificationService
    {
        Task<List<ChallengeAnswerInputModel>> GenerateChallengesForClaimAsync(int claimId);
        Task<bool> SubmitVerificationAnswersAsync(int claimId, List<ChallengeAnswerInputModel> answers, string claimantUserId);
        Task<List<VerificationChallengeResultViewModel>> GetVerificationResultsForClaimAsync(int claimId, bool isPrivilegedReviewer);
        Task<(bool Passed, double AverageScore, string SuggestedOutcome)> EvaluateClaimVerificationAsync(int claimId);
    }
}
