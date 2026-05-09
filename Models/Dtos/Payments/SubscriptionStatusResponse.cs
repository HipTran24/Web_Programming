namespace Web_Project.Models.Dtos.Payments
{
    public class SubscriptionStatusResponse
    {
        public bool IsPremium { get; set; }

        public string PlanCode { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }
    }
}
