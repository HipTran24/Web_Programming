using Microsoft.EntityFrameworkCore;
using Web_Project.Models;

namespace Web_Project.Services.Payments
{
    public class PremiumSubscriptionService : IPremiumSubscriptionService
    {
        private const string PremiumPlanCode = "PREMIUM";
        private readonly AppDbContext _dbContext;

        public PremiumSubscriptionService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PremiumSubscriptionStatus> GetStatusAsync(int userId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var subscription = await _dbContext.UserSubscriptions
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive && x.ExpiresAt > now)
                .OrderByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is not null)
            {
                return new PremiumSubscriptionStatus
                {
                    IsPremium = true,
                    PlanCode = subscription.PlanCode,
                    ExpiresAt = subscription.ExpiresAt
                };
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new { x.IsPremium, x.SubscriptionTier, x.PremiumExpiresAt })
                .FirstOrDefaultAsync(cancellationToken);

            var isPremium = user is not null &&
                (user.IsPremium || string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase)) &&
                (!user.PremiumExpiresAt.HasValue || user.PremiumExpiresAt.Value > now);

            return new PremiumSubscriptionStatus
            {
                IsPremium = isPremium,
                PlanCode = isPremium ? PremiumPlanCode : string.Empty,
                ExpiresAt = isPremium ? user?.PremiumExpiresAt : null
            };
        }

        public async Task GrantPremiumAsync(int userId, int paymentTransactionId, int days, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var safeDays = Math.Max(1, days);
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return;
            }

            var current = await _dbContext.UserSubscriptions
                .Where(x => x.UserId == userId && x.PlanCode == PremiumPlanCode && x.IsActive)
                .OrderByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            DateTime newExpiresAt;

            if (current is null)
            {
                newExpiresAt = now.AddDays(safeDays);
                _dbContext.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    PlanCode = PremiumPlanCode,
                    StartsAt = now,
                    ExpiresAt = newExpiresAt,
                    IsActive = true,
                    PaymentTransactionId = paymentTransactionId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                var baseDate = current.ExpiresAt > now ? current.ExpiresAt : now;
                newExpiresAt = baseDate.AddDays(safeDays);
                current.ExpiresAt = newExpiresAt;
                current.PaymentTransactionId = paymentTransactionId;
                current.UpdatedAt = now;
            }

            user.IsPremium = true;
            user.SubscriptionTier = "Premium";
            user.PremiumStartedAt ??= now;
            user.PremiumExpiresAt = newExpiresAt;
        }
    }
}
