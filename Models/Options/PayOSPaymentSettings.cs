namespace Web_Project.Models
{
    public class PayOSPaymentSettings
    {
        public const string SectionName = "PayOS";

        public bool Enabled { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string ChecksumKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = "https://api-merchant.payos.vn/v2/payment-requests";

        public string PublicBaseUrl { get; set; } = string.Empty;

        public string ReturnPath { get; set; } = "/api/payments/payos/return";

        public string CancelPath { get; set; } = "/api/payments/payos/cancel";

        public string WebhookPath { get; set; } = "/api/payments/payos/webhook";

        public decimal PremiumAmount { get; set; }

        public int PremiumDays { get; set; }

        public string DescriptionPrefix { get; set; } = "SLP";
    }
}
