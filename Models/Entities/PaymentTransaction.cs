using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Project.Models
{
    public class PaymentTransaction
    {
        [Key]
        public int PaymentTransactionId { get; set; }

        public int UserId { get; set; }

        [MaxLength(64)]
        public string Provider { get; set; } = "Mock";

        [MaxLength(64)]
        public string? OrderId { get; set; }

        [MaxLength(64)]
        public string? RequestId { get; set; }

        [MaxLength(32)]
        public string PlanCode { get; set; } = "Premium";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(8)]
        public string Currency { get; set; } = "VND";

        [MaxLength(32)]
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

        [MaxLength(64)]
        public string PlanName { get; set; } = "Premium";

        [MaxLength(128)]
        public string ProviderReference { get; set; } = string.Empty;

        public User User { get; set; } = null!;
    }

    public static class PaymentTransactionStatuses
    {
        public const string Pending = "Pending";
        public const string Success = "Success";
        public const string Paid = "Paid";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }
}
