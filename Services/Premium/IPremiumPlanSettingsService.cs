namespace Web_Project.Services.Premium
{
    public interface IPremiumPlanSettingsService
    {
        Task<PremiumPlanSettings> GetSettingsAsync(CancellationToken cancellationToken);

        Task<PremiumPlanSettings> UpdateSettingsAsync(
            decimal amount,
            int days,
            int updatedByUserId,
            CancellationToken cancellationToken);
    }

    public sealed class PremiumPlanSettings
    {
        public decimal Amount { get; init; }

        public int Days { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public int? UpdatedByUserId { get; init; }
    }
}
