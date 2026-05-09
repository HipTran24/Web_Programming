using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class UserSubscription
    {
        [Key]
        public int UserSubscriptionId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(32)]
        public string PlanCode { get; set; } = "PREMIUM";

        public DateTime StartsAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int? PaymentTransactionId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User User { get; set; } = null!;

        public PaymentTransaction? PaymentTransaction { get; set; }
    }
}
