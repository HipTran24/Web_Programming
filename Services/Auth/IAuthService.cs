using Wed_Project.Models;
using Wed_Project.Services.Otp;

namespace Wed_Project.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginServiceResult> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken);

        Task<RegisterServiceResult> RegisterAsync(
            RegisterRequest request,
            string requestIp,
            CancellationToken cancellationToken);

        Task<OtpVerificationResult> VerifyEmailOtpAsync(
            VerifyEmailOtpRequest request,
            CancellationToken cancellationToken);

        Task<OtpDispatchResult> ResendEmailOtpAsync(
            ResendEmailOtpRequest request,
            string requestIp,
            CancellationToken cancellationToken);
    }

    public sealed class LoginServiceResult
    {
        public bool Success { get; init; }

        public int StatusCode { get; init; } = 400;

        public string Message { get; init; } = string.Empty;

        public LoginResponse? Response { get; init; }

        public Dictionary<string, string[]> ValidationErrors { get; init; } = new();
    }

    public sealed class RegisterServiceResult
    {
        public bool Success { get; init; }

        public bool IsConflict { get; init; }

        public string Message { get; init; } = string.Empty;

        public RegisterResponse? Response { get; init; }

        public Dictionary<string, string[]> ValidationErrors { get; init; } = new();
    }
}
