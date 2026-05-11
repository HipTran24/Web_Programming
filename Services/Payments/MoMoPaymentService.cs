using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Models.Dtos.Payments;

namespace Web_Project.Services.Payments
{
    public class MoMoPaymentService : IMoMoPaymentService
    {
        private const string ProviderName = "MoMo";
        private const string PremiumPlanCode = "PREMIUM_30D";
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPremiumSubscriptionService _premiumSubscriptionService;
        private readonly MoMoPaymentSettings _settings;
        private readonly ILogger<MoMoPaymentService> _logger;

        public MoMoPaymentService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IPremiumSubscriptionService premiumSubscriptionService,
            IOptions<MoMoPaymentSettings> settings,
            ILogger<MoMoPaymentService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _premiumSubscriptionService = premiumSubscriptionService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<CreateMoMoPaymentResponse> CreatePaymentAsync(
            int userId,
            CreateMoMoPaymentRequest request,
            HttpRequest httpRequest,
            CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                return Failure("Thanh toán MoMo hiện chưa được bật.");
            }

            if (!HasRequiredConfiguration())
            {
                return Failure("Thiếu cấu hình MoMo sandbox. Vui lòng kiểm tra PartnerCode, AccessKey và SecretKey.");
            }

            var normalizedPlan = NormalizePlanCode(request.PlanCode);
            if (!string.Equals(normalizedPlan, PremiumPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Gói Premium không hợp lệ.");
            }

            var now = DateTime.UtcNow;
            var amount = Math.Round(Math.Max(1000m, _settings.PremiumAmount), 0, MidpointRounding.AwayFromZero);
            var amountText = amount.ToString("0", CultureInfo.InvariantCulture);
            var orderId = $"SLP{now:yyyyMMddHHmmssfff}{userId}";
            var requestId = $"{orderId}REQ";
            var orderInfo = $"Thanh toán SynapLearn Premium {_settings.PremiumDays} ngày";
            var redirectUrl = BuildAbsoluteUrl(httpRequest, _settings.RedirectPath);
            var ipnUrl = BuildAbsoluteUrl(httpRequest, _settings.IpnPath);
            var extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                userId,
                planCode = normalizedPlan
            })));

            var rawSignature = BuildCreateRawSignature(
                _settings.AccessKey,
                amountText,
                extraData,
                ipnUrl,
                orderId,
                orderInfo,
                _settings.PartnerCode,
                redirectUrl,
                requestId,
                _settings.RequestType);
            var signature = ComputeHmacSha256(rawSignature, _settings.SecretKey);

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                Provider = ProviderName,
                OrderId = orderId,
                RequestId = requestId,
                PlanCode = normalizedPlan,
                Amount = amount,
                Status = PaymentTransactionStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var createRequest = new MoMoCreatePaymentRequest
            {
                PartnerCode = _settings.PartnerCode,
                PartnerName = _settings.PartnerName,
                StoreId = _settings.StoreId,
                RequestId = requestId,
                Amount = amountText,
                OrderId = orderId,
                OrderInfo = orderInfo,
                RedirectUrl = redirectUrl,
                IpnUrl = ipnUrl,
                Lang = string.IsNullOrWhiteSpace(_settings.Lang) ? "vi" : _settings.Lang,
                RequestType = _settings.RequestType,
                AutoCapture = true,
                ExtraData = extraData,
                Signature = signature
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.PostAsJsonAsync(_settings.Endpoint, createRequest, cancellationToken);
                var payload = await response.Content.ReadFromJsonAsync<MoMoCreatePaymentResponse>(cancellationToken: cancellationToken);

                transaction.ProviderResultCode = payload?.ResultCode;
                transaction.ProviderMessage = payload?.Message;
                transaction.PayUrl = payload?.PayUrl;
                transaction.UpdatedAt = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode || payload is null || payload.ResultCode != 0 || string.IsNullOrWhiteSpace(payload.PayUrl))
                {
                    transaction.Status = PaymentTransactionStatuses.Failed;
                    transaction.FailedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return Failure(payload?.Message ?? "MoMo chưa tạo được liên kết thanh toán.");
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return new CreateMoMoPaymentResponse
                {
                    Success = true,
                    Message = "Đã tạo liên kết thanh toán MoMo.",
                    OrderId = orderId,
                    RequestId = requestId,
                    Amount = amount,
                    PayUrl = payload.PayUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create MoMo payment for order {OrderId}", orderId);
                transaction.Status = PaymentTransactionStatuses.Failed;
                transaction.ProviderMessage = "Không thể kết nối MoMo.";
                transaction.FailedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Failure("Không thể kết nối MoMo. Vui lòng thử lại sau.");
            }
        }

        public async Task<MoMoIpnHandleResult> HandleIpnAsync(MoMoPaymentResult payload, CancellationToken cancellationToken)
        {
            return await HandlePaymentResultAsync(payload, cancellationToken);
        }

        public async Task<MoMoIpnHandleResult> HandleSignedReturnAsync(MoMoPaymentResult payload, CancellationToken cancellationToken)
        {
            if (!_settings.AllowRedirectActivationInDevelopment)
            {
                return new MoMoIpnHandleResult { Accepted = false, Message = "Redirect activation is disabled." };
            }

            return await HandlePaymentResultAsync(payload, cancellationToken);
        }

        private async Task<MoMoIpnHandleResult> HandlePaymentResultAsync(MoMoPaymentResult payload, CancellationToken cancellationToken)
        {
            if (!HasRequiredConfiguration())
            {
                return new MoMoIpnHandleResult { Accepted = false, Message = "Missing MoMo configuration." };
            }

            if (!VerifyResultSignature(payload))
            {
                return new MoMoIpnHandleResult { Accepted = false, Message = "Invalid signature." };
            }

            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(x =>
                    x.Provider == ProviderName &&
                    x.OrderId == payload.OrderId &&
                    x.RequestId == payload.RequestId,
                    cancellationToken);

            if (transaction is null)
            {
                return new MoMoIpnHandleResult { Accepted = false, Message = "Transaction not found." };
            }

            if (!string.Equals(payload.PartnerCode, _settings.PartnerCode, StringComparison.Ordinal) ||
                payload.Amount != decimal.ToInt64(transaction.Amount))
            {
                return new MoMoIpnHandleResult { Accepted = false, Message = "Transaction payload mismatch." };
            }

            if (transaction.Status == PaymentTransactionStatuses.Paid)
            {
                return new MoMoIpnHandleResult { Accepted = true, Message = "Transaction already paid." };
            }

            var now = DateTime.UtcNow;
            transaction.ProviderTransactionId = payload.TransId > 0 ? payload.TransId.ToString(CultureInfo.InvariantCulture) : null;
            transaction.ProviderResultCode = payload.ResultCode;
            transaction.ProviderMessage = payload.Message;
            transaction.UpdatedAt = now;

            if (payload.ResultCode == 0)
            {
                transaction.Status = PaymentTransactionStatuses.Paid;
                transaction.PaidAt = now;
                await _premiumSubscriptionService.GrantPremiumAsync(
                    transaction.UserId,
                    transaction.PaymentTransactionId,
                    _settings.PremiumDays,
                    cancellationToken);
            }
            else
            {
                transaction.Status = PaymentTransactionStatuses.Failed;
                transaction.FailedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return new MoMoIpnHandleResult { Accepted = true, Message = "Processed." };
        }

        private bool VerifyResultSignature(MoMoPaymentResult payload)
        {
            var rawSignature =
                $"accessKey={_settings.AccessKey}" +
                $"&amount={payload.Amount}" +
                $"&extraData={payload.ExtraData}" +
                $"&message={payload.Message}" +
                $"&orderId={payload.OrderId}" +
                $"&orderInfo={payload.OrderInfo}" +
                $"&orderType={payload.OrderType}" +
                $"&partnerCode={payload.PartnerCode}" +
                $"&payType={payload.PayType}" +
                $"&requestId={payload.RequestId}" +
                $"&responseTime={payload.ResponseTime}" +
                $"&resultCode={payload.ResultCode}" +
                $"&transId={payload.TransId}";

            var expected = ComputeHmacSha256(rawSignature, _settings.SecretKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(payload.Signature ?? string.Empty));
        }

        private bool HasRequiredConfiguration()
        {
            return !string.IsNullOrWhiteSpace(_settings.PartnerCode) &&
                   !string.IsNullOrWhiteSpace(_settings.AccessKey) &&
                   !string.IsNullOrWhiteSpace(_settings.SecretKey) &&
                   !string.IsNullOrWhiteSpace(_settings.Endpoint);
        }

        private string BuildAbsoluteUrl(HttpRequest request, string path)
        {
            if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            {
                return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
            }

            return $"{request.Scheme}://{request.Host}{path}";
        }

        private static string NormalizePlanCode(string? planCode)
        {
            return string.IsNullOrWhiteSpace(planCode)
                ? PremiumPlanCode
                : planCode.Trim().ToUpperInvariant();
        }

        private static CreateMoMoPaymentResponse Failure(string message)
        {
            return new CreateMoMoPaymentResponse
            {
                Success = false,
                Message = message
            };
        }

        private static string BuildCreateRawSignature(
            string accessKey,
            string amount,
            string extraData,
            string ipnUrl,
            string orderId,
            string orderInfo,
            string partnerCode,
            string redirectUrl,
            string requestId,
            string requestType)
        {
            return $"accessKey={accessKey}" +
                   $"&amount={amount}" +
                   $"&extraData={extraData}" +
                   $"&ipnUrl={ipnUrl}" +
                   $"&orderId={orderId}" +
                   $"&orderInfo={orderInfo}" +
                   $"&partnerCode={partnerCode}" +
                   $"&redirectUrl={redirectUrl}" +
                   $"&requestId={requestId}" +
                   $"&requestType={requestType}";
        }

        private static string ComputeHmacSha256(string rawData, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var dataBytes = Encoding.UTF8.GetBytes(rawData);
            using var hmac = new HMACSHA256(keyBytes);
            return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
        }

        private sealed class MoMoCreatePaymentRequest
        {
            [JsonPropertyName("partnerCode")]
            public string PartnerCode { get; init; } = string.Empty;

            [JsonPropertyName("partnerName")]
            public string PartnerName { get; init; } = string.Empty;

            [JsonPropertyName("storeId")]
            public string StoreId { get; init; } = string.Empty;

            [JsonPropertyName("requestId")]
            public string RequestId { get; init; } = string.Empty;

            [JsonPropertyName("amount")]
            public string Amount { get; init; } = string.Empty;

            [JsonPropertyName("orderId")]
            public string OrderId { get; init; } = string.Empty;

            [JsonPropertyName("orderInfo")]
            public string OrderInfo { get; init; } = string.Empty;

            [JsonPropertyName("redirectUrl")]
            public string RedirectUrl { get; init; } = string.Empty;

            [JsonPropertyName("ipnUrl")]
            public string IpnUrl { get; init; } = string.Empty;

            [JsonPropertyName("lang")]
            public string Lang { get; init; } = "vi";

            [JsonPropertyName("requestType")]
            public string RequestType { get; init; } = string.Empty;

            [JsonPropertyName("autoCapture")]
            public bool AutoCapture { get; init; } = true;

            [JsonPropertyName("extraData")]
            public string ExtraData { get; init; } = string.Empty;

            [JsonPropertyName("signature")]
            public string Signature { get; init; } = string.Empty;
        }

        private sealed class MoMoCreatePaymentResponse
        {
            [JsonPropertyName("resultCode")]
            public int ResultCode { get; init; }

            [JsonPropertyName("message")]
            public string Message { get; init; } = string.Empty;

            [JsonPropertyName("payUrl")]
            public string PayUrl { get; init; } = string.Empty;
        }
    }
}
