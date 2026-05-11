using Microsoft.EntityFrameworkCore;
using Web_Project.Models;

namespace Web_Project.Services.Premium
{
    public sealed class PremiumPaymentService : IPremiumPaymentService
    {
        private const decimal PremiumMonthlyPriceVnd = 99000m;
        private readonly AppDbContext _dbContext;

        public PremiumPaymentService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CheckoutResponse> CreateCheckoutAsync(
            int userId,
            string planName,
            string requestScheme,
            string requestHost,
            CancellationToken cancellationToken)
        {
            var userExists = await _dbContext.Users
                .AnyAsync(x => x.UserId == userId, cancellationToken);

            if (!userExists)
            {
                throw new InvalidOperationException("Không tìm thấy tài khoản để thanh toán.");
            }

            var normalizedPlanName = string.Equals(planName, "Premium", StringComparison.OrdinalIgnoreCase)
                ? "Premium"
                : "Premium";

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                Provider = "Mock",
                Amount = PremiumMonthlyPriceVnd,
                Currency = "VND",
                Status = PaymentTransactionStatuses.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PaidAt = null,
                PlanName = normalizedPlanName,
                PlanCode = normalizedPlanName,
                ProviderReference = $"mock-{Guid.NewGuid():N}"
            };
            transaction.OrderId = transaction.ProviderReference;
            transaction.RequestId = $"{transaction.ProviderReference}-request";

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var origin = $"{requestScheme}://{requestHost}".TrimEnd('/');
            return new CheckoutResponse
            {
                PaymentTransactionId = transaction.PaymentTransactionId,
                PlanName = transaction.PlanName,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Status = transaction.Status,
                CheckoutUrl = $"{origin}/premium/payment-success.html?transactionId={transaction.PaymentTransactionId}"
            };
        }

        public async Task<PaymentTransactionResponse> MarkPaymentSuccessAsync(
            int userId,
            int transactionId,
            CancellationToken cancellationToken)
        {
            var transaction = await _dbContext.PaymentTransactions
                .Include(x => x.User)
                .FirstOrDefaultAsync(
                    x => x.PaymentTransactionId == transactionId && x.UserId == userId,
                    cancellationToken);

            if (transaction is null)
            {
                throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");
            }

            if (!string.Equals(transaction.Status, PaymentTransactionStatuses.Success, StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.UtcNow;
                transaction.Status = PaymentTransactionStatuses.Success;
                transaction.PaidAt = now;
                transaction.UpdatedAt = now;
                transaction.User.IsPremium = true;
                transaction.User.SubscriptionTier = "Premium";
                transaction.User.PremiumStartedAt ??= now;
                transaction.User.PremiumExpiresAt = now.AddDays(30);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Map(transaction);
        }

        public async Task<PaymentTransactionResponse> MarkPaymentFailedAsync(
            int userId,
            int transactionId,
            string status,
            CancellationToken cancellationToken)
        {
            var normalizedStatus = string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                ? PaymentTransactionStatuses.Cancelled
                : PaymentTransactionStatuses.Failed;

            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(
                    x => x.PaymentTransactionId == transactionId && x.UserId == userId,
                    cancellationToken);

            if (transaction is null)
            {
                throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");
            }

            if (!string.Equals(transaction.Status, PaymentTransactionStatuses.Success, StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = normalizedStatus;
                transaction.FailedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Map(transaction);
        }

        private static PaymentTransactionResponse Map(PaymentTransaction transaction)
        {
            return new PaymentTransactionResponse
            {
                PaymentTransactionId = transaction.PaymentTransactionId,
                PlanName = transaction.PlanName,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                PaidAt = transaction.PaidAt
            };
        }
    }
}
