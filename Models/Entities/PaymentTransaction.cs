using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Project.Models
{
    public class PaymentTransaction
    {
        [Key]
        public int PaymentTransactionId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Provider { get; set; } = "MoMo";

        [Required]
        [MaxLength(64)]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string RequestId { get; set; } = string.Empty;

        [Required]
        [MaxLength(32)]
        public string PlanCode { get; set; } = "PREMIUM_30D";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(24)]
        public string Status { get; set; } = PaymentTransactionStatuses.Pending;

        [MaxLength(64)]
        public string? ProviderTransactionId { get; set; }

        [MaxLength(2048)]
        public string? PayUrl { get; set; }

        [MaxLength(512)]
        public string? ProviderMessage { get; set; }

        public int? ProviderResultCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? FailedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User User { get; set; } = null!;
    }

    public static class PaymentTransactionStatuses
    {
        public const string Pending = "Pending";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
    }
}
