using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Models.Dtos.Payments;

namespace Web_Project.Services.Payments
{
    public class PayOSPaymentService : IPayOSPaymentService
    {
        private const string ProviderName = "PayOS";
        private const string PremiumPlanCode = "PREMIUM_30D";
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPremiumSubscriptionService _premiumSubscriptionService;
        private readonly PayOSPaymentSettings _settings;
        private readonly ILogger<PayOSPaymentService> _logger;

        public PayOSPaymentService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IPremiumSubscriptionService premiumSubscriptionService,
            IOptions<PayOSPaymentSettings> settings,
            ILogger<PayOSPaymentService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _premiumSubscriptionService = premiumSubscriptionService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<CreatePayOSPaymentResponse> CreatePaymentAsync(
            int userId,
            CreatePayOSPaymentRequest request,
            HttpRequest httpRequest,
            CancellationToken cancellationToken)
        {
            if (!_settings.Enabled)
            {
                return Failure("Thanh toán PayOS hiện chưa được bật.");
            }

            if (!HasRequiredConfiguration())
            {
                return Failure("Thiếu cấu hình PayOS. Vui lòng kiểm tra ClientId, ApiKey, ChecksumKey và Endpoint.");
            }

            var normalizedPlan = NormalizePlanCode(request.PlanCode);
            if (!string.Equals(normalizedPlan, PremiumPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Gói Premium không hợp lệ.");
            }

            var now = DateTime.UtcNow;
            var amount = Math.Round(Math.Max(1000m, _settings.PremiumAmount), 0, MidpointRounding.AwayFromZero);
            var amountValue = decimal.ToInt64(amount);
            var orderCode = GenerateOrderCode(userId, now);
            var description = BuildDescription(orderCode);
            var returnUrl = BuildAbsoluteUrl(httpRequest, _settings.ReturnPath);
            var cancelUrl = BuildAbsoluteUrl(httpRequest, _settings.CancelPath);
            var rawSignature = BuildCreateRawSignature(amountValue, cancelUrl, description, orderCode, returnUrl);
            var signature = ComputeHmacSha256(rawSignature, _settings.ChecksumKey);

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                Provider = ProviderName,
                OrderId = orderCode.ToString(CultureInfo.InvariantCulture),
                RequestId = orderCode.ToString(CultureInfo.InvariantCulture),
                PlanCode = normalizedPlan,
                Amount = amount,
                Status = PaymentTransactionStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var createRequest = new PayOSCreatePaymentRequest
            {
                OrderCode = orderCode,
                Amount = amountValue,
                Description = description,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
                Signature = signature,
                Items =
                [
                    new PayOSPaymentItem
                    {
                        Name = $"SynapLearn Premium {_settings.PremiumDays} ngày",
                        Quantity = 1,
                        Price = amountValue
                    }
                ]
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint)
                {
                    Content = JsonContent.Create(createRequest)
                };
                requestMessage.Headers.TryAddWithoutValidation("x-client-id", _settings.ClientId);
                requestMessage.Headers.TryAddWithoutValidation("x-api-key", _settings.ApiKey);

                using var response = await client.SendAsync(requestMessage, cancellationToken);
                var payload = await response.Content.ReadFromJsonAsync<PayOSCreatePaymentResponse>(cancellationToken: cancellationToken);

                transaction.ProviderMessage = payload?.Description;
                transaction.PayUrl = payload?.Data?.CheckoutUrl;
                transaction.ProviderReference = payload?.Data?.PaymentLinkId ?? string.Empty;
                transaction.UpdatedAt = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode || payload is null || payload.Code != "00" || string.IsNullOrWhiteSpace(payload.Data?.CheckoutUrl))
                {
                    transaction.Status = PaymentTransactionStatuses.Failed;
                    transaction.FailedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return Failure(payload?.Description ?? "PayOS chưa tạo được liên kết thanh toán.");
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return new CreatePayOSPaymentResponse
                {
                    Success = true,
                    Message = "Đã tạo liên kết thanh toán PayOS.",
                    OrderId = transaction.OrderId,
                    Amount = amount,
                    PayUrl = payload.Data.CheckoutUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create PayOS payment for order {OrderCode}", orderCode);
                transaction.Status = PaymentTransactionStatuses.Failed;
                transaction.ProviderMessage = "Không thể kết nối PayOS.";
                transaction.FailedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Failure("Không thể kết nối PayOS. Vui lòng thử lại sau.");
            }
        }

        public async Task<PayOSWebhookHandleResult> HandleWebhookAsync(PayOSWebhookPayload payload, CancellationToken cancellationToken)
        {
            if (!HasRequiredConfiguration())
            {
                return new PayOSWebhookHandleResult { Accepted = false, Message = "Missing PayOS configuration." };
            }

            if (!VerifyWebhookSignature(payload))
            {
                return new PayOSWebhookHandleResult { Accepted = false, Message = "Invalid signature." };
            }

            var orderId = payload.Data.OrderCode.ToString(CultureInfo.InvariantCulture);
            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(x => x.Provider == ProviderName && x.OrderId == orderId, cancellationToken);

            if (transaction is null)
            {
                return new PayOSWebhookHandleResult { Accepted = false, Message = "Transaction not found." };
            }

            if (payload.Data.Amount != decimal.ToInt64(transaction.Amount))
            {
                return new PayOSWebhookHandleResult { Accepted = false, Message = "Transaction payload mismatch." };
            }

            if (transaction.Status == PaymentTransactionStatuses.Paid)
            {
                return new PayOSWebhookHandleResult { Accepted = true, Message = "Transaction already paid." };
            }

            var now = DateTime.UtcNow;
            transaction.ProviderTransactionId = payload.Data.Reference;
            transaction.ProviderReference = payload.Data.PaymentLinkId;
            transaction.ProviderMessage = payload.Data.DescriptionText;
            transaction.UpdatedAt = now;

            if (payload.Success && payload.Code == "00" && payload.Data.Code == "00")
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
            return new PayOSWebhookHandleResult { Accepted = true, Message = "Processed." };
        }

        private bool VerifyWebhookSignature(PayOSWebhookPayload payload)
        {
            var rawSignature = BuildWebhookRawSignature(payload.Data);
            var expected = ComputeHmacSha256(rawSignature, _settings.ChecksumKey);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(payload.Signature ?? string.Empty));
        }

        private bool HasRequiredConfiguration()
        {
            return !string.IsNullOrWhiteSpace(_settings.ClientId) &&
                   !string.IsNullOrWhiteSpace(_settings.ApiKey) &&
                   !string.IsNullOrWhiteSpace(_settings.ChecksumKey) &&
                   !string.IsNullOrWhiteSpace(_settings.Endpoint) &&
                   !string.IsNullOrWhiteSpace(_settings.ReturnPath) &&
                   !string.IsNullOrWhiteSpace(_settings.CancelPath);
        }

        private string BuildAbsoluteUrl(HttpRequest request, string path)
        {
            if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            {
                return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
            }

            return $"{request.Scheme}://{request.Host}{path}";
        }

        private string BuildDescription(long orderCode)
        {
            var prefix = string.IsNullOrWhiteSpace(_settings.DescriptionPrefix) ? "SLP" : _settings.DescriptionPrefix.Trim();
            var suffix = orderCode.ToString(CultureInfo.InvariantCulture);
            var maxSuffixLength = Math.Max(1, 9 - prefix.Length);
            if (suffix.Length > maxSuffixLength)
            {
                suffix = suffix[^maxSuffixLength..];
            }

            return $"{prefix}{suffix}"[..Math.Min(9, $"{prefix}{suffix}".Length)];
        }

        private static long GenerateOrderCode(int userId, DateTime now)
        {
            return long.Parse(now.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string BuildCreateRawSignature(long amount, string cancelUrl, string description, long orderCode, string returnUrl)
        {
            return $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        }

        private static string BuildWebhookRawSignature(PayOSWebhookData data)
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["accountNumber"] = data.AccountNumber,
                ["amount"] = data.Amount.ToString(CultureInfo.InvariantCulture),
                ["code"] = data.Code,
                ["counterAccountBankId"] = data.CounterAccountBankId,
                ["counterAccountBankName"] = data.CounterAccountBankName,
                ["counterAccountName"] = data.CounterAccountName,
                ["counterAccountNumber"] = data.CounterAccountNumber,
                ["currency"] = data.Currency,
                ["desc"] = data.DescriptionText,
                ["description"] = data.Description,
                ["orderCode"] = data.OrderCode.ToString(CultureInfo.InvariantCulture),
                ["paymentLinkId"] = data.PaymentLinkId,
                ["reference"] = data.Reference,
                ["transactionDateTime"] = data.TransactionDateTime,
                ["virtualAccountName"] = data.VirtualAccountName,
                ["virtualAccountNumber"] = data.VirtualAccountNumber
            };

            return string.Join("&", values.Select(x => $"{x.Key}={x.Value}"));
        }

        private static string ComputeHmacSha256(string rawData, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string NormalizePlanCode(string? planCode)
        {
            return string.IsNullOrWhiteSpace(planCode) ? PremiumPlanCode : planCode.Trim().ToUpperInvariant();
        }

        private static CreatePayOSPaymentResponse Failure(string message)
        {
            return new CreatePayOSPaymentResponse
            {
                Success = false,
                Message = message
            };
        }

        private sealed class PayOSCreatePaymentRequest
        {
            [JsonPropertyName("orderCode")]
            public long OrderCode { get; init; }

            [JsonPropertyName("amount")]
            public long Amount { get; init; }

            [JsonPropertyName("description")]
            public string Description { get; init; } = string.Empty;

            [JsonPropertyName("items")]
            public List<PayOSPaymentItem> Items { get; init; } = [];

            [JsonPropertyName("cancelUrl")]
            public string CancelUrl { get; init; } = string.Empty;

            [JsonPropertyName("returnUrl")]
            public string ReturnUrl { get; init; } = string.Empty;

            [JsonPropertyName("signature")]
            public string Signature { get; init; } = string.Empty;
        }

        private sealed class PayOSPaymentItem
        {
            [JsonPropertyName("name")]
            public string Name { get; init; } = string.Empty;

            [JsonPropertyName("quantity")]
            public int Quantity { get; init; }

            [JsonPropertyName("price")]
            public long Price { get; init; }
        }

        private sealed class PayOSCreatePaymentResponse
        {
            [JsonPropertyName("code")]
            public string Code { get; init; } = string.Empty;

            [JsonPropertyName("desc")]
            public string Description { get; init; } = string.Empty;

            [JsonPropertyName("data")]
            public PayOSCreatePaymentData? Data { get; init; }
        }

        private sealed class PayOSCreatePaymentData
        {
            [JsonPropertyName("paymentLinkId")]
            public string PaymentLinkId { get; init; } = string.Empty;

            [JsonPropertyName("checkoutUrl")]
            public string CheckoutUrl { get; init; } = string.Empty;
        }
    }
}
