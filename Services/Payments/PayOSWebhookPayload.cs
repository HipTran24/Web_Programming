using System.Text.Json.Serialization;

namespace Web_Project.Services.Payments
{
    public sealed class PayOSWebhookPayload
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("desc")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public PayOSWebhookData Data { get; set; } = new();

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public sealed class PayOSWebhookData
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("accountNumber")]
        public string AccountNumber { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("transactionDateTime")]
        public string TransactionDateTime { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("paymentLinkId")]
        public string PaymentLinkId { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("desc")]
        public string DescriptionText { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountBankId")]
        public string CounterAccountBankId { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountBankName")]
        public string CounterAccountBankName { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountName")]
        public string CounterAccountName { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountNumber")]
        public string CounterAccountNumber { get; set; } = string.Empty;

        [JsonPropertyName("virtualAccountName")]
        public string VirtualAccountName { get; set; } = string.Empty;

        [JsonPropertyName("virtualAccountNumber")]
        public string VirtualAccountNumber { get; set; } = string.Empty;
    }
}
