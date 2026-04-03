using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Web_Project.Models;

namespace Web_Project.Services.AI
{
    public class MultiModelSummaryService : IGroqSummaryService
    {
        private const string ProviderGemini = "gemini";
        private const string ProviderGroq = "groq";
        private static readonly ConcurrentDictionary<string, ProviderHealthState> ProviderHealthStates = new();

        private readonly GeminiSummaryService _gemini;
        private readonly GroqSummaryService _groq;
        private readonly IAiRuntimeSettingsService _runtimeSettings;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MultiModelSummaryService> _logger;

        public MultiModelSummaryService(
            GeminiSummaryService gemini,
            GroqSummaryService groq,
            IAiRuntimeSettingsService runtimeSettings,
            IMemoryCache cache,
            ILogger<MultiModelSummaryService> logger)
        {
            _gemini = gemini;
            _groq = groq;
            _runtimeSettings = runtimeSettings;
            _cache = cache;
            _logger = logger;
        }

        public async Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            var snapshot = _runtimeSettings.GetSnapshot();
            var estimatedTokens = EstimateTokens(text, snapshot.Routing.TextOutputTokenBudget, snapshot.Routing);
            var providerOrder = ResolveProviderOrder(snapshot.Routing, estimatedTokens);

            return await ExecuteWithFallbackAsync(
                providerOrder,
                estimatedTokens,
                snapshot,
                callGemini: providerToken => _gemini.SummarizeTextAsync(text, sourceHint, providerToken),
                callGroq: providerToken => _groq.SummarizeTextAsync(text, sourceHint, providerToken),
                operationName: "summarize-text",
                cancellationToken);
        }

        public async Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken)
        {
            var snapshot = _runtimeSettings.GetSnapshot();
            var estimatedTokens = EstimateTokensForImage(snapshot.Routing.ImageOutputTokenBudget);
            var providerOrder = ResolveProviderOrder(
                new AiRoutingSettings
                {
                    PrimaryTextProvider = snapshot.Routing.PrimaryVisionProvider,
                    PreferFastestHealthyProvider = snapshot.Routing.PreferFastestHealthyProvider
                },
                estimatedTokens);

            return await ExecuteWithFallbackAsync(
                providerOrder,
                estimatedTokens,
                snapshot,
                callGemini: providerToken => _gemini.SummarizeImageAsync(imageBytes, mimeType, fileName, providerToken),
                callGroq: providerToken => _groq.SummarizeImageAsync(imageBytes, mimeType, fileName, providerToken),
                operationName: "summarize-image",
                cancellationToken);
        }

        public Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            // Audio transcription is currently served by Groq in this project.
            return _groq.TranscribeAudioAsync(audioFilePath, cancellationToken);
        }

        public async Task<AiQuizResult> GenerateQuizAsync(
            string sourceText,
            int totalQuestions,
            string difficulty,
            string quizType,
            CancellationToken cancellationToken)
        {
            var snapshot = _runtimeSettings.GetSnapshot();
            var estimatedTokens = EstimateTokens(sourceText, snapshot.Routing.QuizOutputTokenBudget, snapshot.Routing);
            var providerOrder = ResolveProviderOrder(snapshot.Routing, estimatedTokens);

            return await ExecuteWithFallbackAsync(
                providerOrder,
                estimatedTokens,
                snapshot,
                callGemini: providerToken => _gemini.GenerateQuizAsync(sourceText, totalQuestions, difficulty, quizType, providerToken),
                callGroq: providerToken => _groq.GenerateQuizAsync(sourceText, totalQuestions, difficulty, quizType, providerToken),
                operationName: "generate-quiz",
                cancellationToken);
        }

        private async Task<T> ExecuteWithFallbackAsync<T>(
            IReadOnlyList<string> providerOrder,
            int estimatedTokens,
            AiRuntimeSettingsSnapshot snapshot,
            Func<CancellationToken, Task<T>> callGemini,
            Func<CancellationToken, Task<T>> callGroq,
            string operationName,
            CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var providerTimeoutMs = Math.Clamp(snapshot.Routing.ProviderExecutionTimeoutMs, 1500, 30_000);

            foreach (var provider in providerOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsProviderCoolingDown(provider, snapshot.Routing, out var coolingReason))
                {
                    errors.Add($"{provider}: {coolingReason}");
                    continue;
                }

                if (!CanUseProviderForRequest(provider, snapshot, out var providerGateReason))
                {
                    errors.Add($"{provider}: {providerGateReason}");
                    continue;
                }

                if (!TryConsumeDailyBudget(provider, estimatedTokens, snapshot.Routing, out var budgetReason))
                {
                    errors.Add($"{provider}: {budgetReason}");
                    continue;
                }

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    providerCts.CancelAfter(providerTimeoutMs);
                    var result = provider == ProviderGemini
                        ? await callGemini(providerCts.Token)
                        : await callGroq(providerCts.Token);
                    stopwatch.Stop();

                    RecordProviderSuccess(provider, stopwatch.ElapsedMilliseconds, operationName);

                    _logger.LogInformation(
                        "AI routing selected provider {Provider} for {Operation} (estimated tokens: {EstimatedTokens})",
                        provider,
                        operationName,
                        estimatedTokens);

                    return result;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var timeoutEx = new TimeoutException(
                        $"Provider {provider} timeout sau {providerTimeoutMs}ms cho {operationName}.");
                    RecordProviderFailure(provider, timeoutEx, operationName);
                    errors.Add($"{provider}: timeout {providerTimeoutMs}ms");
                    _logger.LogWarning(
                        "AI provider {Provider} timed out for {Operation} after {TimeoutMs}ms. Trying fallback.",
                        provider,
                        operationName,
                        providerTimeoutMs);
                }
                catch (Exception ex)
                {
                    RecordProviderFailure(provider, ex, operationName);
                    errors.Add($"{provider}: {ex.Message}");
                    _logger.LogWarning(
                        ex,
                        "AI provider {Provider} failed for {Operation}. Trying fallback if available.",
                        provider,
                        operationName);
                }
            }

            var combined = string.Join(" | ", errors);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(combined)
                    ? "Không có AI provider khả dụng để xử lý yêu cầu."
                    : $"Không có AI provider khả dụng: {combined}");
        }

        private IReadOnlyList<string> ResolveProviderOrder(AiRoutingSettings routing, int estimatedTokens)
        {
            var primary = NormalizeProvider(routing.PrimaryTextProvider);

            var ordered = primary switch
            {
                ProviderGemini => new List<string> { ProviderGemini, ProviderGroq },
                ProviderGroq => new List<string> { ProviderGroq, ProviderGemini },
                _ => new List<string> { ProviderGemini, ProviderGroq }
            };

            if (routing.PreferFastestHealthyProvider)
            {
                ordered = ReorderByHealthAndLatency(ordered, estimatedTokens, routing);
            }

            return ordered;
        }

        private List<string> ReorderByHealthAndLatency(List<string> candidates, int estimatedTokens, AiRoutingSettings routing)
        {
            var snapshot = _runtimeSettings.GetSnapshot();
            return candidates
                .OrderBy(provider => IsProviderCoolingDown(provider, routing, out _) ? 1 : 0)
                .ThenBy(provider => CanUseProviderForRequest(provider, snapshot, out _) ? 0 : 1)
                .ThenBy(provider => GetProviderLatencyRank(provider))
                .ToList();
        }

        private double GetProviderLatencyRank(string provider)
        {
            var state = ProviderHealthStates.GetOrAdd(provider, _ => new ProviderHealthState());
            lock (state.SyncRoot)
            {
                return state.SampleCount > 0 ? state.EwmaLatencyMs : double.MaxValue;
            }
        }

        private static bool CanUseProviderForRequest(string provider, AiRuntimeSettingsSnapshot snapshot, out string reason)
        {
            reason = string.Empty;

            if (provider == ProviderGemini)
            {
                if (string.IsNullOrWhiteSpace(snapshot.Gemini.ApiKey))
                {
                    reason = "Thiếu Gemini:ApiKey.";
                    return false;
                }

                return true;
            }

            if (provider == ProviderGroq)
            {
                if (string.IsNullOrWhiteSpace(snapshot.Groq.GroqApiKey))
                {
                    reason = "Thiếu Groq:GroqApiKey.";
                    return false;
                }

                return true;
            }

            reason = "Provider không hợp lệ.";
            return false;
        }

        private bool TryConsumeDailyBudget(string provider, int estimatedTokens, AiRoutingSettings routing, out string reason)
        {
            reason = string.Empty;
            if (!routing.EnforceDailyTokenBudget)
            {
                return true;
            }

            var limit = provider switch
            {
                ProviderGemini => routing.GeminiDailyTokenBudget,
                ProviderGroq => routing.GroqDailyTokenBudget,
                _ => 0
            };

            if (limit <= 0)
            {
                return true;
            }

            var reserve = Math.Clamp(routing.MinReservedTokensPerProvider, 0, Math.Max(0, limit - 1));
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var cacheKey = $"ai:token-usage:{provider}:{today}";
            var usedTokens = _cache.TryGetValue(cacheKey, out int cached) ? cached : 0;
            var effectiveLimit = Math.Max(0, limit - reserve);

            if (usedTokens + estimatedTokens > effectiveLimit)
            {
                reason =
                    $"Ngân sách token ngày cho {provider} sắp hết ({usedTokens}/{limit}, reserve {reserve}).";
                return false;
            }

            var nextUtcDay = DateTime.UtcNow.Date.AddDays(1);
            _cache.Set(cacheKey, usedTokens + estimatedTokens, nextUtcDay - DateTime.UtcNow + TimeSpan.FromMinutes(5));
            return true;
        }

        private static bool IsProviderCoolingDown(string provider, AiRoutingSettings routing, out string reason)
        {
            reason = string.Empty;
            if (!routing.EnableProviderHealthSwitch)
            {
                return false;
            }

            var state = ProviderHealthStates.GetOrAdd(provider, _ => new ProviderHealthState());
            lock (state.SyncRoot)
            {
                if (state.CooldownUntilUtc <= DateTime.UtcNow)
                {
                    return false;
                }

                var remain = (int)Math.Ceiling((state.CooldownUntilUtc - DateTime.UtcNow).TotalSeconds);
                reason = $"Provider đang cooldown {Math.Max(remain, 1)}s do chậm/lỗi liên tiếp.";
                return true;
            }
        }

        private void RecordProviderSuccess(string provider, long elapsedMs, string operationName)
        {
            var routing = _runtimeSettings.GetSnapshot().Routing;
            if (!routing.EnableProviderHealthSwitch)
            {
                return;
            }

            var state = ProviderHealthStates.GetOrAdd(provider, _ => new ProviderHealthState());
            var slowThresholdMs = Math.Clamp(routing.SlowRequestThresholdMs, 1000, 60_000);
            var slowStreakThreshold = Math.Clamp(routing.SlowRequestStreakThreshold, 1, 5);
            var cooldownSeconds = Math.Clamp(routing.ProviderCooldownSeconds, 5, 300);

            lock (state.SyncRoot)
            {
                state.ConsecutiveFailures = 0;

                // EWMA latency for logs/inspection.
                if (state.SampleCount == 0)
                {
                    state.EwmaLatencyMs = elapsedMs;
                    state.SampleCount = 1;
                }
                else
                {
                    state.EwmaLatencyMs = (0.25d * elapsedMs) + (0.75d * state.EwmaLatencyMs);
                    state.SampleCount += 1;
                }

                if (elapsedMs > slowThresholdMs)
                {
                    state.ConsecutiveSlowRequests += 1;
                }
                else
                {
                    state.ConsecutiveSlowRequests = 0;
                    state.CooldownUntilUtc = DateTime.MinValue;
                }

                if (state.ConsecutiveSlowRequests >= slowStreakThreshold)
                {
                    state.CooldownUntilUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
                    _logger.LogWarning(
                        "Provider {Provider} marked cooldown for {CooldownSeconds}s after slow response {ElapsedMs}ms on {Operation}",
                        provider,
                        cooldownSeconds,
                        elapsedMs,
                        operationName);
                }
            }
        }

        private void RecordProviderFailure(string provider, Exception ex, string operationName)
        {
            var routing = _runtimeSettings.GetSnapshot().Routing;
            if (!routing.EnableProviderHealthSwitch)
            {
                return;
            }

            var state = ProviderHealthStates.GetOrAdd(provider, _ => new ProviderHealthState());
            var failureThreshold = Math.Clamp(routing.ConsecutiveFailureThreshold, 1, 10);
            var cooldownSeconds = Math.Clamp(routing.ProviderCooldownSeconds, 5, 300);

            lock (state.SyncRoot)
            {
                state.ConsecutiveFailures += 1;
                state.ConsecutiveSlowRequests = 0;

                if (state.ConsecutiveFailures >= failureThreshold)
                {
                    state.CooldownUntilUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
                    _logger.LogWarning(
                        ex,
                        "Provider {Provider} marked cooldown for {CooldownSeconds}s after {FailureCount} consecutive failures on {Operation}",
                        provider,
                        cooldownSeconds,
                        state.ConsecutiveFailures,
                        operationName);
                }
            }
        }

        private static int EstimateTokens(string content, int outputBudget, AiRoutingSettings routing)
        {
            var charsPerToken = Math.Clamp(routing.ApproxCharsPerToken, 2, 8);
            var text = content ?? string.Empty;
            var inputTokens = (int)Math.Ceiling(text.Length / (double)charsPerToken);
            var outputTokens = Math.Max(200, outputBudget);
            return Math.Max(1, inputTokens + outputTokens);
        }

        private static int EstimateTokensForImage(int outputBudget)
        {
            // Coarse estimate for image understanding prompts.
            return Math.Max(1_200, 1_200 + Math.Max(200, outputBudget));
        }

        private static string NormalizeProvider(string provider)
        {
            var value = (provider ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                ProviderGemini => ProviderGemini,
                ProviderGroq => ProviderGroq,
                _ => string.Empty
            };
        }

        private sealed class ProviderHealthState
        {
            public object SyncRoot { get; } = new();

            public int ConsecutiveFailures { get; set; }

            public int ConsecutiveSlowRequests { get; set; }

            public DateTime CooldownUntilUtc { get; set; } = DateTime.MinValue;

            public double EwmaLatencyMs { get; set; }

            public int SampleCount { get; set; }
        }
    }
}
