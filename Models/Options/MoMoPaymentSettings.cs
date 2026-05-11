namespace Web_Project.Models
{
    public class MoMoPaymentSettings
    {
        public const string SectionName = "MoMo";

        public bool Enabled { get; set; }

        public string PartnerCode { get; set; } = string.Empty;

        public string AccessKey { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public string PublicBaseUrl { get; set; } = string.Empty;

        public string RedirectPath { get; set; } = string.Empty;

        public string IpnPath { get; set; } = string.Empty;

        public decimal PremiumAmount { get; set; }

        public int PremiumDays { get; set; }

        public string Lang { get; set; } = string.Empty;

        public string RequestType { get; set; } = string.Empty;

        public string PartnerName { get; set; } = string.Empty;

        public string StoreId { get; set; } = string.Empty;

        public bool AllowRedirectActivationInDevelopment { get; set; }
    }
}
