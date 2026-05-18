using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Models.Dtos.Payments;
using Web_Project.Services.Premium;

namespace Web_Project.Services.Payments
{
    public class PayOSPaymentService : IPayOSPaymentService
    {
        private const string ProviderName = "PayOS";
        private const string PremiumPlanCode = "PREMIUM_30D";
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPremiumSubscriptionService _premiumSubscriptionService;
        private readonly IPremiumPlanSettingsService _premiumPlanSettingsService;
        private readonly PayOSPaymentSettings _settings;
        private readonly ILogger<PayOSPaymentService> _logger;

        public PayOSPaymentService(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IPremiumSubscriptionService premiumSubscriptionService,
            IPremiumPlanSettingsService premiumPlanSettingsService,
            IOptions<PayOSPaymentSettings> settings,
            ILogger<PayOSPaymentService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _premiumSubscriptionService = premiumSubscriptionService;
            _premiumPlanSettingsService = premiumPlanSettingsService;
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
                return Failure("Thanh toan PayOS hien chua duoc bat.");
            }

            if (!HasRequiredConfiguration())
            {
                return Failure("Thieu cau hinh PayOS. Vui long kiem tra ClientId, ApiKey, ChecksumKey va Endpoint.");
            }

            var normalizedPlan = NormalizePlanCode(request.PlanCode);
            if (!string.Equals(normalizedPlan, PremiumPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Goi Premium khong hop le.");
            }

            var now = DateTime.UtcNow;
            var planSettings = await _premiumPlanSettingsService.GetSettingsAsync(cancellationToken);
            var configuredAmount = Math.Round(Math.Max(0m, planSettings.Amount), 0, MidpointRounding.AwayFromZero);
            var orderCode = GenerateOrderCode(userId, now);
            var description = BuildDescription(orderCode);
            var returnUrl = BuildAbsoluteUrl(httpRequest, _settings.ReturnPath);
            var cancelUrl = BuildAbsoluteUrl(httpRequest, _settings.CancelPath);

            if (configuredAmount <= 0m)
            {
                var freeTransaction = new PaymentTransaction
                {
                    UserId = userId,
                    Provider = ProviderName,
                    OrderId = orderCode.ToString(CultureInfo.InvariantCulture),
                    RequestId = orderCode.ToString(CultureInfo.InvariantCulture),
                    PlanCode = normalizedPlan,
                    PlanName = $"Premium {planSettings.Days} ngay",
                    Amount = 0m,
                    Currency = "VND",
                    Status = PaymentTransactionStatuses.Paid,
                    ProviderMessage = "Admin configured Premium price as 0 VND.",
                    CreatedAt = now,
                    PaidAt = now,
                    UpdatedAt = now
                };

                _dbContext.PaymentTransactions.Add(freeTransaction);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _premiumSubscriptionService.GrantPremiumAsync(
                    userId,
                    freeTransaction.PaymentTransactionId,
                    planSettings.Days,
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new CreatePayOSPaymentResponse
                {
                    Success = true,
                    Message = "Da kich hoat Premium 0d theo cau hinh admin.",
                    OrderId = freeTransaction.OrderId ?? string.Empty,
                    Amount = 0m,
                    PayUrl = "/premium/payment-success.html?payment=success&provider=payos&message=Premium%200d%20da%20duoc%20kich%20hoat"
                };
            }

            var amount = Math.Round(Math.Max(1000m, configuredAmount), 0, MidpointRounding.AwayFromZero);
            var amountValue = decimal.ToInt64(amount);
            var rawSignature = BuildCreateRawSignature(amountValue, cancelUrl, description, orderCode, returnUrl);
            var signature = ComputeHmacSha256(rawSignature, _settings.ChecksumKey);

            var transaction = new PaymentTransaction
            {
                UserId = userId,
                Provider = ProviderName,
                OrderId = orderCode.ToString(CultureInfo.InvariantCulture),
                RequestId = orderCode.ToString(CultureInfo.InvariantCulture),
                PlanCode = normalizedPlan,
                PlanName = $"Premium {planSettings.Days} ngay",
                Amount = amount,
                Currency = "VND",
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
                        Name = $"SynapLearn Premium {planSettings.Days} ngay",
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
                    return Failure(payload?.Description ?? "PayOS chua tao duoc lien ket thanh toan.");
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return new CreatePayOSPaymentResponse
                {
                    Success = true,
                    Message = "Da tao lien ket thanh toan PayOS.",
                    OrderId = transaction.OrderId ?? string.Empty,
                    Amount = amount,
                    PayUrl = payload.Data.CheckoutUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create PayOS payment for order {OrderCode}", orderCode);
                transaction.Status = PaymentTransactionStatuses.Failed;
                transaction.ProviderMessage = "Khong the ket noi PayOS.";
                transaction.FailedAt = DateTime.UtcNow;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return Failure("Khong the ket noi PayOS. Vui long thu lai sau.");
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
                var planSettings = await _premiumPlanSettingsService.GetSettingsAsync(cancellationToken);
                await _premiumSubscriptionService.GrantPremiumAsync(
                    transaction.UserId,
                    transaction.PaymentTransactionId,
                    planSettings.Days,
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

        public async Task<PayOSPaymentSyncResult> HandleReturnAsync(
            string? orderCode,
            string? paymentLinkId,
            CancellationToken cancellationToken)
        {
            if (!HasRequiredConfiguration())
            {
                return SyncFailure("Missing PayOS configuration.");
            }

            var normalizedOrderCode = orderCode?.Trim();
            var normalizedPaymentLinkId = paymentLinkId?.Trim();

            var query = _dbContext.PaymentTransactions
                .Where(x => x.Provider == ProviderName);

            PaymentTransaction? transaction = null;
            if (!string.IsNullOrWhiteSpace(normalizedOrderCode))
            {
                transaction = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(x => x.OrderId == normalizedOrderCode, cancellationToken);
            }

            if (transaction is null && !string.IsNullOrWhiteSpace(normalizedPaymentLinkId))
            {
                transaction = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(x => x.ProviderReference == normalizedPaymentLinkId, cancellationToken);
            }

            if (transaction is null)
            {
                return SyncFailure("Transaction not found.");
            }

            return await SyncPaymentTransactionAsync(transaction, normalizedPaymentLinkId, cancellationToken);
        }

        public async Task<PayOSPaymentSyncResult> SyncLatestPendingPaymentAsync(int userId, CancellationToken cancellationToken)
        {
            if (!HasRequiredConfiguration())
            {
                return SyncFailure("Missing PayOS configuration.");
            }

            var pendingTransactions = await _dbContext.PaymentTransactions
                .Where(x => x.UserId == userId &&
                            x.Provider == ProviderName &&
                            x.Status == PaymentTransactionStatuses.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .Take(3)
                .ToListAsync(cancellationToken);

            PayOSPaymentSyncResult? lastResult = null;
            foreach (var transaction in pendingTransactions)
            {
                lastResult = await SyncPaymentTransactionAsync(transaction, null, cancellationToken);
                if (lastResult.Paid)
                {
                    return lastResult;
                }
            }

            return lastResult ?? SyncFailure("No pending PayOS transaction.");
        }

        private async Task<PayOSPaymentSyncResult> SyncPaymentTransactionAsync(
            PaymentTransaction transaction,
            string? preferredPaymentRequestId,
            CancellationToken cancellationToken)
        {
            if (transaction.Status == PaymentTransactionStatuses.Paid)
            {
                return new PayOSPaymentSyncResult
                {
                    Accepted = true,
                    Paid = true,
                    OrderId = transaction.OrderId ?? string.Empty,
                    Message = "Transaction already paid."
                };
            }

            var paymentRequestId = !string.IsNullOrWhiteSpace(preferredPaymentRequestId)
                ? preferredPaymentRequestId
                : !string.IsNullOrWhiteSpace(transaction.OrderId)
                    ? transaction.OrderId
                    : transaction.ProviderReference;

            if (string.IsNullOrWhiteSpace(paymentRequestId))
            {
                return SyncFailure("Missing payment request id.");
            }

            var payload = await GetPaymentLinkInformationAsync(paymentRequestId, cancellationToken);
            if (payload is null || payload.Code != "00" || payload.Data is null)
            {
                return SyncFailure(payload?.Description ?? "Cannot retrieve PayOS payment link.");
            }

            if (!string.Equals(transaction.OrderId, payload.Data.OrderCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return SyncFailure("Transaction order mismatch.");
            }

            if (payload.Data.Amount != decimal.ToInt64(transaction.Amount))
            {
                return SyncFailure("Transaction amount mismatch.");
            }

            var now = DateTime.UtcNow;
            transaction.ProviderReference = string.IsNullOrWhiteSpace(payload.Data.Id)
                ? transaction.ProviderReference
                : payload.Data.Id;
            transaction.ProviderMessage = string.IsNullOrWhiteSpace(payload.Description)
                ? payload.Data.Status
                : $"{payload.Description} - {payload.Data.Status}";
            transaction.UpdatedAt = now;

            if (string.Equals(payload.Data.Status, "PAID", StringComparison.OrdinalIgnoreCase) &&
                payload.Data.AmountPaid >= payload.Data.Amount &&
                payload.Data.AmountRemaining <= 0)
            {
                transaction.Status = PaymentTransactionStatuses.Paid;
                transaction.PaidAt = now;
                transaction.FailedAt = null;

                var planSettings = await _premiumPlanSettingsService.GetSettingsAsync(cancellationToken);
                await _premiumSubscriptionService.GrantPremiumAsync(
                    transaction.UserId,
                    transaction.PaymentTransactionId,
                    planSettings.Days,
                    cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return new PayOSPaymentSyncResult
                {
                    Accepted = true,
                    Paid = true,
                    OrderId = transaction.OrderId ?? string.Empty,
                    Message = "PayOS payment is paid."
                };
            }

            if (string.Equals(payload.Data.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.Data.Status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = PaymentTransactionStatuses.Failed;
                transaction.FailedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PayOSPaymentSyncResult
            {
                Accepted = true,
                Paid = false,
                OrderId = transaction.OrderId ?? string.Empty,
                Message = $"PayOS status is {payload.Data.Status}."
            };
        }

        private async Task<PayOSPaymentLinkResponse?> GetPaymentLinkInformationAsync(
            string paymentRequestId,
            CancellationToken cancellationToken)
        {
            var endpoint = $"{_settings.Endpoint.TrimEnd('/')}/{Uri.EscapeDataString(paymentRequestId)}";
            var client = _httpClientFactory.CreateClient();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, endpoint);
            requestMessage.Headers.TryAddWithoutValidation("x-client-id", _settings.ClientId);
            requestMessage.Headers.TryAddWithoutValidation("x-api-key", _settings.ApiKey);

            using var response = await client.SendAsync(requestMessage, cancellationToken);
            return await response.Content.ReadFromJsonAsync<PayOSPaymentLinkResponse>(cancellationToken: cancellationToken);
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

            var description = $"{prefix}{suffix}";
            return description[..Math.Min(9, description.Length)];
        }

        private static long GenerateOrderCode(int userId, DateTime now)
        {
            return long.Parse($"{now:yyMMddHHmmss}{userId % 1000:000}", CultureInfo.InvariantCulture);
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

        private static PayOSPaymentSyncResult SyncFailure(string message)
        {
            return new PayOSPaymentSyncResult
            {
                Accepted = false,
                Paid = false,
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

        private sealed class PayOSPaymentLinkResponse
        {
            [JsonPropertyName("code")]
            public string Code { get; init; } = string.Empty;

            [JsonPropertyName("desc")]
            public string Description { get; init; } = string.Empty;

            [JsonPropertyName("data")]
            public PayOSPaymentLinkData? Data { get; init; }
        }

        private sealed class PayOSPaymentLinkData
        {
            [JsonPropertyName("id")]
            public string Id { get; init; } = string.Empty;

            [JsonPropertyName("orderCode")]
            public long OrderCode { get; init; }

            [JsonPropertyName("amount")]
            public long Amount { get; init; }

            [JsonPropertyName("amountPaid")]
            public long AmountPaid { get; init; }

            [JsonPropertyName("amountRemaining")]
            public long AmountRemaining { get; init; }

            [JsonPropertyName("status")]
            public string Status { get; init; } = string.Empty;
        }
    }
}
