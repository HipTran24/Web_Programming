namespace Web_Project.Services.Payments
{
    public interface IPremiumSubscriptionService
    {
        Task<PremiumSubscriptionStatus> GetStatusAsync(int userId, CancellationToken cancellationToken);

        Task GrantPremiumAsync(int userId, int paymentTransactionId, int days, CancellationToken cancellationToken);
    }

    public sealed class PremiumSubscriptionStatus
    {
        public bool IsPremium { get; init; }

        public string PlanCode { get; init; } = string.Empty;

        public DateTime? ExpiresAt { get; init; }
    }
}
