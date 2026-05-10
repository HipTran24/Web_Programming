namespace Web_Project.Models.Dtos.Payments
{
    public class CreateMoMoPaymentResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string PayUrl { get; set; } = string.Empty;
    }
}
