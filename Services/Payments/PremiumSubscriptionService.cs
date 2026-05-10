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

            return new PremiumSubscriptionStatus
            {
                IsPremium = subscription is not null,
                PlanCode = subscription?.PlanCode ?? string.Empty,
                ExpiresAt = subscription?.ExpiresAt
            };
        }

        public async Task GrantPremiumAsync(int userId, int paymentTransactionId, int days, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var safeDays = Math.Max(1, days);
            var current = await _dbContext.UserSubscriptions
                .Where(x => x.UserId == userId && x.PlanCode == PremiumPlanCode && x.IsActive)
                .OrderByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (current is null)
            {
                _dbContext.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    PlanCode = PremiumPlanCode,
                    StartsAt = now,
                    ExpiresAt = now.AddDays(safeDays),
                    IsActive = true,
                    PaymentTransactionId = paymentTransactionId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                return;
            }

            var baseDate = current.ExpiresAt > now ? current.ExpiresAt : now;
            current.ExpiresAt = baseDate.AddDays(safeDays);
            current.PaymentTransactionId = paymentTransactionId;
            current.UpdatedAt = now;
        }
    }
}
