using Web_Project.Models.Dtos.Payments;

namespace Web_Project.Services.Payments
{
    public interface IPayOSPaymentService
    {
        Task<CreatePayOSPaymentResponse> CreatePaymentAsync(
            int userId,
            CreatePayOSPaymentRequest request,
            HttpRequest httpRequest,
            CancellationToken cancellationToken);

        Task<PayOSWebhookHandleResult> HandleWebhookAsync(PayOSWebhookPayload payload, CancellationToken cancellationToken);

        Task<PayOSPaymentSyncResult> HandleReturnAsync(
            string? orderCode,
            string? paymentLinkId,
            CancellationToken cancellationToken);

        Task<PayOSPaymentSyncResult> SyncLatestPendingPaymentAsync(int userId, CancellationToken cancellationToken);
    }

    public sealed class PayOSWebhookHandleResult
    {
        public bool Accepted { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public sealed class PayOSPaymentSyncResult
    {
        public bool Accepted { get; set; }

        public bool Paid { get; set; }

        public string Message { get; set; } = string.Empty;

        public string OrderId { get; set; } = string.Empty;
    }
}
