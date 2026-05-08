using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class PaymentTransaction
    {
        [Key]
        public int PaymentTransactionId { get; set; }

        public int UserId { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(8)]
        public string Currency { get; set; } = "VND";

        [MaxLength(32)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        [MaxLength(64)]
        public string PlanName { get; set; } = "Premium";

        [MaxLength(128)]
        public string ProviderReference { get; set; } = string.Empty;

        public User User { get; set; } = null!;
    }
}
