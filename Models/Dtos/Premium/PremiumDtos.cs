namespace Web_Project.Models
{
    public sealed class PremiumStatusResponse
    {
        public bool IsPremium { get; set; }

        public string SubscriptionTier { get; set; } = "Normal";

        public DateTime? PremiumStartedAt { get; set; }

        public DateTime? PremiumExpiresAt { get; set; }

        public int DailyTokenLimit { get; set; }

        public int TokenUsedToday { get; set; }

        public DateTime TokenUsageDate { get; set; }

        public int TokenRemainingToday => Math.Max(0, DailyTokenLimit - TokenUsedToday);
    }

    public sealed class CheckoutRequest
    {
        public string PlanName { get; set; } = "Premium";
    }

    public sealed class CheckoutResponse
    {
        public int PaymentTransactionId { get; set; }

        public string PlanName { get; set; } = "Premium";

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "VND";

        public string Status { get; set; } = "Pending";

        public string CheckoutUrl { get; set; } = string.Empty;
    }

    public sealed class PaymentTransactionResponse
    {
        public int PaymentTransactionId { get; set; }

        public string PlanName { get; set; } = "Premium";

        public decimal Amount { get; set; }

        public string Currency { get; set; } = "VND";

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }
    }
}
