using Web_Project.Models.Dtos.Payments;

namespace Web_Project.Services.Payments
{
    public interface IMoMoPaymentService
    {
        Task<CreateMoMoPaymentResponse> CreatePaymentAsync(
            int userId,
            CreateMoMoPaymentRequest request,
            HttpRequest httpRequest,
            CancellationToken cancellationToken);

        Task<MoMoIpnHandleResult> HandleIpnAsync(MoMoPaymentResult payload, CancellationToken cancellationToken);

        Task<MoMoIpnHandleResult> HandleSignedReturnAsync(MoMoPaymentResult payload, CancellationToken cancellationToken);
    }

    public sealed class MoMoIpnHandleResult
    {
        public bool Accepted { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
