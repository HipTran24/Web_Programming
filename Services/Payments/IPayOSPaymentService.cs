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
    }

    public sealed class PayOSWebhookHandleResult
    {
        public bool Accepted { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
