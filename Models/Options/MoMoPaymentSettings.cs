namespace Web_Project.Models
{
    public class MoMoPaymentSettings
    {
        public const string SectionName = "MoMo";

        public bool Enabled { get; set; } = true;

        public string PartnerCode { get; set; } = string.Empty;

        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";

        public string PublicBaseUrl { get; set; } = string.Empty;

        public string RedirectPath { get; set; } = "/api/payments/momo/return";

        public string IpnPath { get; set; } = "/api/payments/momo/ipn";

        public decimal PremiumAmount { get; set; } = 10000m;

        public int PremiumDays { get; set; } = 30;

        public string Lang { get; set; } = "vi";

        public string RequestType { get; set; } = "captureWallet";

        public string PartnerName { get; set; } = "SynapLearn";

        public string StoreId { get; set; } = "SynapLearn";

        public bool AllowRedirectActivationInDevelopment { get; set; }
    }
}
