using Web_Project.Models;

namespace Web_Project.Services.Premium
{
    public interface IUserTokenQuotaService
    {
        const int NormalDailyTokenLimit = 200_000;
        const int PremiumDailyTokenLimit = 500_000;

        int EstimateTextTokens(string text, int outputBudget = 1_500);

        int EstimateImageTokens(int outputBudget = 900);

        int EstimateQuizTokens(string sourceText, int totalQuestions);

        Task<PremiumStatusResponse> GetStatusAsync(int userId, CancellationToken cancellationToken);

        Task EnsureCanConsumeAsync(
            int? userId,
            int estimatedTokens,
            string featureName,
            CancellationToken cancellationToken);
    }

    public sealed class TokenQuotaExceededException : InvalidOperationException
    {
        public TokenQuotaExceededException(string message, int limit, int usedToday)
            : base(message)
        {
            Limit = limit;
            UsedToday = usedToday;
        }

        public int Limit { get; }

        public int UsedToday { get; }
    }
}
