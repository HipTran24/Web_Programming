using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;

namespace Web_Project.Services.AI
{
    public sealed class AiRuntimeSettingsService : IAiRuntimeSettingsService
    {
        private const string SettingKey = "system:ai:runtime-config";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GeminiSettings _defaultGemini;
        private readonly GroqSettings _defaultGroq;
        private readonly AiRoutingSettings _defaultRouting;
        private readonly object _syncRoot = new();
        private AiRuntimeSettingsSnapshot? _cachedSnapshot;

        public AiRuntimeSettingsService(
            IServiceScopeFactory scopeFactory,
            IOptions<GeminiSettings> defaultGemini,
            IOptions<GroqSettings> defaultGroq,
            IOptions<AiRoutingSettings> defaultRouting)
        {
            _scopeFactory = scopeFactory;
            _defaultGemini = Clone(defaultGemini.Value);
            _defaultGroq = Clone(defaultGroq.Value);
            _defaultRouting = Clone(defaultRouting.Value);
        }

        public AiRuntimeSettingsSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                _cachedSnapshot ??= LoadSnapshotFromStore();
                return Clone(_cachedSnapshot);
            }
        }

        public async Task<AdminAiSystemSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken)
        {
            var snapshot = await LoadSnapshotFromStoreAsync(cancellationToken);
            CacheSnapshot(snapshot);
            return MapToResponse(snapshot);
        }

        public async Task<AdminAiSystemSettingsResponse> UpdateAdminSettingsAsync(
            AdminAiSystemSettingsUpdateRequest request,
            int adminUserId,
            CancellationToken cancellationToken)
        {
            var current = await LoadSnapshotFromStoreAsync(cancellationToken);
            var next = ApplyRequest(current, request, adminUserId);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var setting = await dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == SettingKey, cancellationToken);

            var now = DateTime.UtcNow;
            var payload = JsonSerializer.Serialize(new StoredAiRuntimeSettings
            {
                Gemini = Clone(next.Gemini),
                Groq = Clone(next.Groq),
                Routing = Clone(next.Routing)
            }, JsonOptions);

            if (setting is null)
            {
                setting = new SystemSetting
                {
                    SettingKey = SettingKey,
                    SettingValue = payload,
                    Description = "Admin AI runtime configuration",
                    UpdatedByUserId = adminUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                dbContext.SystemSettings.Add(setting);
            }
            else
            {
                setting.SettingValue = payload;
                setting.Description = "Admin AI runtime configuration";
                setting.UpdatedByUserId = adminUserId;
                setting.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var persisted = new AiRuntimeSettingsSnapshot
            {
                Gemini = Clone(next.Gemini),
                Groq = Clone(next.Groq),
                Routing = Clone(next.Routing),
                UpdatedAt = setting.UpdatedAt,
                UpdatedByUserId = setting.UpdatedByUserId
            };

            CacheSnapshot(persisted);
            return MapToResponse(persisted);
        }

        private AiRuntimeSettingsSnapshot LoadSnapshotFromStore()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var setting = dbContext.SystemSettings
                .AsNoTracking()
                .FirstOrDefault(x => x.SettingKey == SettingKey);

            return BuildSnapshot(setting);
        }

        private async Task<AiRuntimeSettingsSnapshot> LoadSnapshotFromStoreAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var setting = await dbContext.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SettingKey == SettingKey, cancellationToken);

            return BuildSnapshot(setting);
        }

        private AiRuntimeSettingsSnapshot BuildSnapshot(SystemSetting? setting)
        {
            if (string.IsNullOrWhiteSpace(setting?.SettingValue))
            {
                return new AiRuntimeSettingsSnapshot
                {
                    Gemini = Clone(_defaultGemini),
                    Groq = Clone(_defaultGroq),
                    Routing = Clone(_defaultRouting),
                    UpdatedAt = setting?.UpdatedAt,
                    UpdatedByUserId = setting?.UpdatedByUserId
                };
            }

            try
            {
                var stored = JsonSerializer.Deserialize<StoredAiRuntimeSettings>(setting.SettingValue, JsonOptions) ?? new StoredAiRuntimeSettings();
                return new AiRuntimeSettingsSnapshot
                {
                    Gemini = MergeGemini(_defaultGemini, stored.Gemini),
                    Groq = MergeGroq(_defaultGroq, stored.Groq),
                    Routing = MergeRouting(_defaultRouting, stored.Routing),
                    UpdatedAt = setting.UpdatedAt,
                    UpdatedByUserId = setting.UpdatedByUserId
                };
            }
            catch
            {
                return new AiRuntimeSettingsSnapshot
                {
                    Gemini = Clone(_defaultGemini),
                    Groq = Clone(_defaultGroq),
                    Routing = Clone(_defaultRouting),
                    UpdatedAt = setting?.UpdatedAt,
                    UpdatedByUserId = setting?.UpdatedByUserId
                };
            }
        }

        private void CacheSnapshot(AiRuntimeSettingsSnapshot snapshot)
        {
            lock (_syncRoot)
            {
                _cachedSnapshot = Clone(snapshot);
            }
        }

        private static AdminAiSystemSettingsResponse MapToResponse(AiRuntimeSettingsSnapshot snapshot)
        {
            return new AdminAiSystemSettingsResponse
            {
                Routing = new AdminAiRoutingSettingsDto
                {
                    PrimaryTextProvider = snapshot.Routing.PrimaryTextProvider,
                    PrimaryVisionProvider = snapshot.Routing.PrimaryVisionProvider,
                    EnforceDailyTokenBudget = snapshot.Routing.EnforceDailyTokenBudget,
                    ApproxCharsPerToken = snapshot.Routing.ApproxCharsPerToken,
                    TextOutputTokenBudget = snapshot.Routing.TextOutputTokenBudget,
                    QuizOutputTokenBudget = snapshot.Routing.QuizOutputTokenBudget,
                    ImageOutputTokenBudget = snapshot.Routing.ImageOutputTokenBudget,
                    GeminiDailyTokenBudget = snapshot.Routing.GeminiDailyTokenBudget,
                    GroqDailyTokenBudget = snapshot.Routing.GroqDailyTokenBudget,
                    MinReservedTokensPerProvider = snapshot.Routing.MinReservedTokensPerProvider,
                    EnableProviderHealthSwitch = snapshot.Routing.EnableProviderHealthSwitch,
                    SlowRequestThresholdMs = snapshot.Routing.SlowRequestThresholdMs,
                    SlowRequestStreakThreshold = snapshot.Routing.SlowRequestStreakThreshold,
                    ConsecutiveFailureThreshold = snapshot.Routing.ConsecutiveFailureThreshold,
                    ProviderCooldownSeconds = snapshot.Routing.ProviderCooldownSeconds,
                    PreferFastestHealthyProvider = snapshot.Routing.PreferFastestHealthyProvider,
                    ProviderExecutionTimeoutMs = snapshot.Routing.ProviderExecutionTimeoutMs
                },
                Gemini = new AdminGeminiSettingsDto
                {
                    HasApiKey = !string.IsNullOrWhiteSpace(snapshot.Gemini.ApiKey),
                    BaseUrl = snapshot.Gemini.BaseUrl,
                    TextModel = snapshot.Gemini.TextModel,
                    VisionModel = snapshot.Gemini.VisionModel,
                    MaxInputCharacters = snapshot.Gemini.MaxInputCharacters,
                    MaxQuizInputCharacters = snapshot.Gemini.MaxQuizInputCharacters,
                    RequestTimeoutSeconds = snapshot.Gemini.RequestTimeoutSeconds,
                    MaxModelCandidates = snapshot.Gemini.MaxModelCandidates,
                    MaxRetriesPerModel = snapshot.Gemini.MaxRetriesPerModel,
                    FallbackModels = [.. snapshot.Gemini.FallbackModels]
                },
                Groq = new AdminGroqSettingsDto
                {
                    HasApiKey = !string.IsNullOrWhiteSpace(snapshot.Groq.GroqApiKey),
                    BaseUrl = snapshot.Groq.BaseUrl,
                    TextModel = snapshot.Groq.TextModel,
                    VisionModel = snapshot.Groq.VisionModel,
                    AudioModel = snapshot.Groq.AudioModel,
                    MaxInputCharacters = snapshot.Groq.MaxInputCharacters,
                    MaxQuizInputCharacters = snapshot.Groq.MaxQuizInputCharacters,
                    RequestTimeoutSeconds = snapshot.Groq.RequestTimeoutSeconds,
                    MaxModelCandidates = snapshot.Groq.MaxModelCandidates,
                    MaxConcurrentRequests = snapshot.Groq.MaxConcurrentRequests,
                    QueueWaitTimeoutSeconds = snapshot.Groq.QueueWaitTimeoutSeconds,
                    MaxRetriesPerModel = snapshot.Groq.MaxRetriesPerModel,
                    EnableResponseCache = snapshot.Groq.EnableResponseCache,
                    ResponseCacheDays = ResolveGroqResponseCacheDays(snapshot.Groq),
                    FallbackModels = [.. snapshot.Groq.FallbackModels]
                },
                UpdatedAt = snapshot.UpdatedAt,
                UpdatedByUserId = snapshot.UpdatedByUserId
            };
        }

        private static AiRuntimeSettingsSnapshot ApplyRequest(
            AiRuntimeSettingsSnapshot current,
            AdminAiSystemSettingsUpdateRequest request,
            int adminUserId)
        {
            var nextGemini = Clone(current.Gemini);
            nextGemini.ApiKey = request.Gemini.ClearApiKey
                ? string.Empty
                : string.IsNullOrWhiteSpace(request.Gemini.ApiKey)
                    ? current.Gemini.ApiKey
                    : request.Gemini.ApiKey.Trim();
            nextGemini.BaseUrl = NormalizeUrl(request.Gemini.BaseUrl, current.Gemini.BaseUrl);
            nextGemini.TextModel = NormalizeText(request.Gemini.TextModel, current.Gemini.TextModel, 128);
            nextGemini.VisionModel = NormalizeText(request.Gemini.VisionModel, current.Gemini.VisionModel, 128);
            nextGemini.MaxInputCharacters = Math.Max(0, request.Gemini.MaxInputCharacters);
            nextGemini.MaxQuizInputCharacters = Math.Max(0, request.Gemini.MaxQuizInputCharacters);
            nextGemini.RequestTimeoutSeconds = Math.Clamp(request.Gemini.RequestTimeoutSeconds, 5, 120);
            nextGemini.MaxModelCandidates = Math.Clamp(request.Gemini.MaxModelCandidates, 1, 5);
            nextGemini.MaxRetriesPerModel = Math.Clamp(request.Gemini.MaxRetriesPerModel, 0, 3);
            nextGemini.FallbackModels = NormalizeModels(request.Gemini.FallbackModels);

            var nextGroq = Clone(current.Groq);
            nextGroq.GroqApiKey = request.Groq.ClearApiKey
                ? string.Empty
                : string.IsNullOrWhiteSpace(request.Groq.ApiKey)
                    ? current.Groq.GroqApiKey
                    : request.Groq.ApiKey.Trim();
            nextGroq.BaseUrl = NormalizeUrl(request.Groq.BaseUrl, current.Groq.BaseUrl);
            nextGroq.TextModel = NormalizeText(request.Groq.TextModel, current.Groq.TextModel, 128);
            nextGroq.VisionModel = NormalizeText(request.Groq.VisionModel, current.Groq.VisionModel, 128);
            nextGroq.AudioModel = NormalizeText(request.Groq.AudioModel, current.Groq.AudioModel, 128);
            nextGroq.MaxInputCharacters = Math.Max(0, request.Groq.MaxInputCharacters);
            nextGroq.MaxQuizInputCharacters = Math.Max(0, request.Groq.MaxQuizInputCharacters);
            nextGroq.RequestTimeoutSeconds = Math.Clamp(request.Groq.RequestTimeoutSeconds, 5, 120);
            nextGroq.MaxModelCandidates = Math.Clamp(request.Groq.MaxModelCandidates, 1, 5);
            nextGroq.MaxConcurrentRequests = Math.Clamp(request.Groq.MaxConcurrentRequests, 1, 16);
            nextGroq.QueueWaitTimeoutSeconds = Math.Clamp(request.Groq.QueueWaitTimeoutSeconds, 1, 60);
            nextGroq.MaxRetriesPerModel = Math.Clamp(request.Groq.MaxRetriesPerModel, 0, 3);
            nextGroq.EnableResponseCache = request.Groq.EnableResponseCache;
            nextGroq.ResponseCacheDays = Math.Clamp(request.Groq.ResponseCacheDays, 1, 30);
            nextGroq.ResponseCacheMinutes = nextGroq.ResponseCacheDays * 1440;
            nextGroq.FallbackModels = NormalizeModels(request.Groq.FallbackModels);

            var nextRouting = Clone(current.Routing);
            nextRouting.PrimaryTextProvider = NormalizeProvider(request.Routing.PrimaryTextProvider, current.Routing.PrimaryTextProvider);
            nextRouting.PrimaryVisionProvider = NormalizeProvider(request.Routing.PrimaryVisionProvider, current.Routing.PrimaryVisionProvider);
            nextRouting.EnforceDailyTokenBudget = request.Routing.EnforceDailyTokenBudget;
            nextRouting.ApproxCharsPerToken = Math.Clamp(request.Routing.ApproxCharsPerToken, 2, 8);
            nextRouting.TextOutputTokenBudget = Math.Clamp(request.Routing.TextOutputTokenBudget, 100, 10_000);
            nextRouting.QuizOutputTokenBudget = Math.Clamp(request.Routing.QuizOutputTokenBudget, 100, 10_000);
            nextRouting.ImageOutputTokenBudget = Math.Clamp(request.Routing.ImageOutputTokenBudget, 100, 10_000);
            nextRouting.GeminiDailyTokenBudget = Math.Max(0, request.Routing.GeminiDailyTokenBudget);
            nextRouting.GroqDailyTokenBudget = Math.Max(0, request.Routing.GroqDailyTokenBudget);
            nextRouting.MinReservedTokensPerProvider = Math.Max(0, request.Routing.MinReservedTokensPerProvider);
            nextRouting.EnableProviderHealthSwitch = request.Routing.EnableProviderHealthSwitch;
            nextRouting.SlowRequestThresholdMs = Math.Clamp(request.Routing.SlowRequestThresholdMs, 1000, 60_000);
            nextRouting.SlowRequestStreakThreshold = Math.Clamp(request.Routing.SlowRequestStreakThreshold, 1, 5);
            nextRouting.ConsecutiveFailureThreshold = Math.Clamp(request.Routing.ConsecutiveFailureThreshold, 1, 10);
            nextRouting.ProviderCooldownSeconds = Math.Clamp(request.Routing.ProviderCooldownSeconds, 5, 300);
            nextRouting.PreferFastestHealthyProvider = request.Routing.PreferFastestHealthyProvider;
            nextRouting.ProviderExecutionTimeoutMs = Math.Clamp(request.Routing.ProviderExecutionTimeoutMs, 1500, 30_000);

            return new AiRuntimeSettingsSnapshot
            {
                Gemini = nextGemini,
                Groq = nextGroq,
                Routing = nextRouting,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = adminUserId
            };
        }

        private static string NormalizeText(string value, string fallback, int maxLength)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallback;
            }

            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static string NormalizeUrl(string value, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static string NormalizeProvider(string value, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized is "groq" or "gemini" ? normalized : fallback;
        }

        private static List<string> NormalizeModels(IEnumerable<string>? values)
        {
            return (values ?? [])
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();
        }

        private static GeminiSettings MergeGemini(GeminiSettings defaults, GeminiSettings? stored)
        {
            if (stored is null)
            {
                return Clone(defaults);
            }

            var merged = Clone(defaults);
            merged.ApiKey = string.IsNullOrWhiteSpace(stored.ApiKey) ? defaults.ApiKey : stored.ApiKey;
            merged.BaseUrl = string.IsNullOrWhiteSpace(stored.BaseUrl) ? defaults.BaseUrl : stored.BaseUrl;
            merged.TextModel = string.IsNullOrWhiteSpace(stored.TextModel) ? defaults.TextModel : stored.TextModel;
            merged.VisionModel = string.IsNullOrWhiteSpace(stored.VisionModel) ? defaults.VisionModel : stored.VisionModel;
            merged.MaxInputCharacters = stored.MaxInputCharacters;
            merged.MaxQuizInputCharacters = stored.MaxQuizInputCharacters;
            merged.RequestTimeoutSeconds = stored.RequestTimeoutSeconds;
            merged.MaxModelCandidates = stored.MaxModelCandidates;
            merged.MaxRetriesPerModel = stored.MaxRetriesPerModel;
            merged.FallbackModels = NormalizeModels(stored.FallbackModels).Count > 0
                ? NormalizeModels(stored.FallbackModels)
                : NormalizeModels(defaults.FallbackModels);
            return merged;
        }

        private static GroqSettings MergeGroq(GroqSettings defaults, GroqSettings? stored)
        {
            if (stored is null)
            {
                return Clone(defaults);
            }

            var merged = Clone(defaults);
            merged.GroqApiKey = string.IsNullOrWhiteSpace(stored.GroqApiKey) ? defaults.GroqApiKey : stored.GroqApiKey;
            merged.BaseUrl = string.IsNullOrWhiteSpace(stored.BaseUrl) ? defaults.BaseUrl : stored.BaseUrl;
            merged.TextModel = string.IsNullOrWhiteSpace(stored.TextModel) ? defaults.TextModel : stored.TextModel;
            merged.VisionModel = string.IsNullOrWhiteSpace(stored.VisionModel) ? defaults.VisionModel : stored.VisionModel;
            merged.AudioModel = string.IsNullOrWhiteSpace(stored.AudioModel) ? defaults.AudioModel : stored.AudioModel;
            merged.MaxInputCharacters = stored.MaxInputCharacters;
            merged.MaxQuizInputCharacters = stored.MaxQuizInputCharacters;
            merged.RequestTimeoutSeconds = stored.RequestTimeoutSeconds;
            merged.MaxModelCandidates = stored.MaxModelCandidates;
            merged.MaxConcurrentRequests = stored.MaxConcurrentRequests;
            merged.QueueWaitTimeoutSeconds = stored.QueueWaitTimeoutSeconds;
            merged.MaxRetriesPerModel = stored.MaxRetriesPerModel;
            merged.EnableResponseCache = stored.EnableResponseCache;
            merged.ResponseCacheDays = ResolveGroqResponseCacheDays(stored, defaults);
            merged.ResponseCacheMinutes = merged.ResponseCacheDays * 1440;
            merged.FallbackModels = NormalizeModels(stored.FallbackModels).Count > 0
                ? NormalizeModels(stored.FallbackModels)
                : NormalizeModels(defaults.FallbackModels);
            return merged;
        }

        private static AiRoutingSettings MergeRouting(AiRoutingSettings defaults, AiRoutingSettings? stored)
        {
            if (stored is null)
            {
                return Clone(defaults);
            }

            var merged = Clone(defaults);
            merged.PrimaryTextProvider = NormalizeProvider(stored.PrimaryTextProvider, defaults.PrimaryTextProvider);
            merged.PrimaryVisionProvider = NormalizeProvider(stored.PrimaryVisionProvider, defaults.PrimaryVisionProvider);
            merged.EnforceDailyTokenBudget = stored.EnforceDailyTokenBudget;
            merged.ApproxCharsPerToken = stored.ApproxCharsPerToken;
            merged.TextOutputTokenBudget = stored.TextOutputTokenBudget;
            merged.QuizOutputTokenBudget = stored.QuizOutputTokenBudget;
            merged.ImageOutputTokenBudget = stored.ImageOutputTokenBudget;
            merged.GeminiDailyTokenBudget = stored.GeminiDailyTokenBudget;
            merged.GroqDailyTokenBudget = stored.GroqDailyTokenBudget;
            merged.MinReservedTokensPerProvider = stored.MinReservedTokensPerProvider;
            merged.EnableProviderHealthSwitch = stored.EnableProviderHealthSwitch;
            merged.SlowRequestThresholdMs = stored.SlowRequestThresholdMs;
            merged.SlowRequestStreakThreshold = stored.SlowRequestStreakThreshold;
            merged.ConsecutiveFailureThreshold = stored.ConsecutiveFailureThreshold;
            merged.ProviderCooldownSeconds = stored.ProviderCooldownSeconds;
            merged.PreferFastestHealthyProvider = stored.PreferFastestHealthyProvider;
            merged.ProviderExecutionTimeoutMs = stored.ProviderExecutionTimeoutMs;
            return merged;
        }

        private static GeminiSettings Clone(GeminiSettings source)
        {
            return new GeminiSettings
            {
                ApiKey = source.ApiKey,
                BaseUrl = source.BaseUrl,
                TextModel = source.TextModel,
                VisionModel = source.VisionModel,
                MaxInputCharacters = source.MaxInputCharacters,
                MaxQuizInputCharacters = source.MaxQuizInputCharacters,
                RequestTimeoutSeconds = source.RequestTimeoutSeconds,
                MaxModelCandidates = source.MaxModelCandidates,
                MaxRetriesPerModel = source.MaxRetriesPerModel,
                FallbackModels = [.. source.FallbackModels]
            };
        }

        private static GroqSettings Clone(GroqSettings source)
        {
            return new GroqSettings
            {
                GroqApiKey = source.GroqApiKey,
                BaseUrl = source.BaseUrl,
                TextModel = source.TextModel,
                VisionModel = source.VisionModel,
                AudioModel = source.AudioModel,
                MaxInputCharacters = source.MaxInputCharacters,
                MaxQuizInputCharacters = source.MaxQuizInputCharacters,
                RequestTimeoutSeconds = source.RequestTimeoutSeconds,
                MaxModelCandidates = source.MaxModelCandidates,
                MaxConcurrentRequests = source.MaxConcurrentRequests,
                QueueWaitTimeoutSeconds = source.QueueWaitTimeoutSeconds,
                MaxRetriesPerModel = source.MaxRetriesPerModel,
                EnableResponseCache = source.EnableResponseCache,
                ResponseCacheDays = ResolveGroqResponseCacheDays(source),
                ResponseCacheMinutes = source.ResponseCacheMinutes > 0
                    ? source.ResponseCacheMinutes
                    : ResolveGroqResponseCacheDays(source) * 1440,
                FallbackModels = [.. source.FallbackModels]
            };
        }

        private static int ResolveGroqResponseCacheDays(GroqSettings source, GroqSettings? fallback = null)
        {
            if (source.ResponseCacheDays > 0)
            {
                return Math.Clamp(source.ResponseCacheDays, 1, 30);
            }

            if (source.ResponseCacheMinutes > 0)
            {
                return Math.Clamp((int)Math.Ceiling(source.ResponseCacheMinutes / 1440d), 1, 30);
            }

            if (fallback is not null)
            {
                if (fallback.ResponseCacheDays > 0)
                {
                    return Math.Clamp(fallback.ResponseCacheDays, 1, 30);
                }

                if (fallback.ResponseCacheMinutes > 0)
                {
                    return Math.Clamp((int)Math.Ceiling(fallback.ResponseCacheMinutes / 1440d), 1, 30);
                }
            }

            return 7;
        }

        private static AiRoutingSettings Clone(AiRoutingSettings source)
        {
            return new AiRoutingSettings
            {
                PrimaryTextProvider = source.PrimaryTextProvider,
                PrimaryVisionProvider = source.PrimaryVisionProvider,
                EnforceDailyTokenBudget = source.EnforceDailyTokenBudget,
                ApproxCharsPerToken = source.ApproxCharsPerToken,
                TextOutputTokenBudget = source.TextOutputTokenBudget,
                QuizOutputTokenBudget = source.QuizOutputTokenBudget,
                ImageOutputTokenBudget = source.ImageOutputTokenBudget,
                GeminiDailyTokenBudget = source.GeminiDailyTokenBudget,
                GroqDailyTokenBudget = source.GroqDailyTokenBudget,
                MinReservedTokensPerProvider = source.MinReservedTokensPerProvider,
                EnableProviderHealthSwitch = source.EnableProviderHealthSwitch,
                SlowRequestThresholdMs = source.SlowRequestThresholdMs,
                SlowRequestStreakThreshold = source.SlowRequestStreakThreshold,
                ConsecutiveFailureThreshold = source.ConsecutiveFailureThreshold,
                ProviderCooldownSeconds = source.ProviderCooldownSeconds,
                PreferFastestHealthyProvider = source.PreferFastestHealthyProvider,
                ProviderExecutionTimeoutMs = source.ProviderExecutionTimeoutMs
            };
        }

        private static AiRuntimeSettingsSnapshot Clone(AiRuntimeSettingsSnapshot source)
        {
            return new AiRuntimeSettingsSnapshot
            {
                Gemini = Clone(source.Gemini),
                Groq = Clone(source.Groq),
                Routing = Clone(source.Routing),
                UpdatedAt = source.UpdatedAt,
                UpdatedByUserId = source.UpdatedByUserId
            };
        }

        private sealed class StoredAiRuntimeSettings
        {
            public GeminiSettings? Gemini { get; set; }

            public GroqSettings? Groq { get; set; }

            public AiRoutingSettings? Routing { get; set; }
        }
    }
}
