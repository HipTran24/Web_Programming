using Microsoft.EntityFrameworkCore;
using Web_Project.Models;

namespace Web_Project.Services.Premium
{
    public sealed class UserTokenQuotaService : IUserTokenQuotaService
    {
        private const int ApproxCharsPerToken = 4;
        private readonly AppDbContext _dbContext;

        public UserTokenQuotaService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public int EstimateTextTokens(string text, int outputBudget = 1_500)
        {
            var normalized = text ?? string.Empty;
            var inputTokens = (int)Math.Ceiling(normalized.Length / (double)ApproxCharsPerToken);
            var chunkCount = Math.Max(1, (int)Math.Ceiling(normalized.Length / 12_000d));
            var mergeReserve = chunkCount > 1 ? 1_500 : 0;
            return Math.Max(1, inputTokens + (chunkCount * Math.Max(200, outputBudget)) + mergeReserve);
        }

        public int EstimateImageTokens(int outputBudget = 900)
        {
            return Math.Max(1_000, outputBudget + 1_200);
        }

        public int EstimateQuizTokens(string sourceText, int totalQuestions)
        {
            var inputTokens = (int)Math.Ceiling((sourceText ?? string.Empty).Length / (double)ApproxCharsPerToken);
            var questionBudget = Math.Clamp(totalQuestions, 1, 30) * 180;
            return Math.Max(1, inputTokens + 1_200 + questionBudget);
        }

        public async Task<PremiumStatusResponse> GetStatusAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new
                {
                    x.IsPremium,
                    x.SubscriptionTier,
                    x.PremiumStartedAt,
                    x.PremiumExpiresAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                throw new InvalidOperationException("Không tìm thấy tài khoản.");
            }

            var today = DateTime.UtcNow.Date;
            var tokenUsed = await _dbContext.DailyUsageCounters
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.UsageDate == today)
                .Select(x => (int?)x.TokenUsed)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            var isPremium = IsPremiumActive(
                user.IsPremium,
                user.SubscriptionTier,
                user.PremiumExpiresAt);

            return new PremiumStatusResponse
            {
                IsPremium = isPremium,
                SubscriptionTier = isPremium ? "Premium" : "Normal",
                PremiumStartedAt = user.PremiumStartedAt,
                PremiumExpiresAt = user.PremiumExpiresAt,
                DailyTokenLimit = isPremium
                    ? IUserTokenQuotaService.PremiumDailyTokenLimit
                    : IUserTokenQuotaService.NormalDailyTokenLimit,
                TokenUsedToday = tokenUsed,
                TokenUsageDate = today
            };
        }

        public async Task EnsureCanConsumeAsync(
            int? userId,
            int estimatedTokens,
            string featureName,
            CancellationToken cancellationToken)
        {
            if (!userId.HasValue)
            {
                return;
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            if (user is null)
            {
                throw new InvalidOperationException("Không tìm thấy tài khoản.");
            }

            var isPremium = IsPremiumActive(user.IsPremium, user.SubscriptionTier, user.PremiumExpiresAt);
            var limit = isPremium
                ? IUserTokenQuotaService.PremiumDailyTokenLimit
                : IUserTokenQuotaService.NormalDailyTokenLimit;
            var requestedTokens = Math.Max(1, estimatedTokens);
            var today = DateTime.UtcNow.Date;

            var counter = await _dbContext.DailyUsageCounters
                .FirstOrDefaultAsync(
                    x => x.UserId == userId.Value && x.UsageDate == today,
                    cancellationToken);

            if (counter is null)
            {
                counter = new DailyUsageCounter
                {
                    UsageDate = today,
                    UserId = userId.Value,
                    GuestSessionId = null,
                    UploadCount = 0,
                    AIProcessCount = 0,
                    QuizGenerationCount = 0,
                    TokenUsed = 0,
                    TotalProcessingTime = 0,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.DailyUsageCounters.Add(counter);
            }

            if (counter.TokenUsed + requestedTokens > limit)
            {
                var message = isPremium
                    ? "Bạn đã dùng hết 500k token hôm nay. Hãy quay lại vào ngày mai để tiếp tục dùng AI Premium."
                    : "Bạn đã dùng hết 200k token hôm nay. Nâng cấp Premium để có 500k token/ngày và học sâu hơn.";

                throw new TokenQuotaExceededException(message, limit, counter.TokenUsed);
            }

            counter.TokenUsed += requestedTokens;
            counter.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsPremiumActive(bool isPremium, string subscriptionTier, DateTime? premiumExpiresAt)
        {
            if (!isPremium && !string.Equals(subscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !premiumExpiresAt.HasValue || premiumExpiresAt.Value > DateTime.UtcNow;
        }
    }
}
