using Web_Project.Models;

namespace Web_Project.Services.Premium
{
    public interface IPremiumPaymentService
    {
        Task<CheckoutResponse> CreateCheckoutAsync(
            int userId,
            string planName,
            string requestScheme,
            string requestHost,
            CancellationToken cancellationToken);

        Task<PaymentTransactionResponse> MarkPaymentSuccessAsync(
            int userId,
            int transactionId,
            CancellationToken cancellationToken);

        Task<PaymentTransactionResponse> MarkPaymentFailedAsync(
            int userId,
            int transactionId,
            string status,
            CancellationToken cancellationToken);
    }
}
