using Microsoft.AspNetCore.Http;
using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;
using Web_Project.Services.AI;
using Web_Project.Services.Content;

namespace Web_Project.Tests.TestDoubles;

internal sealed class FakeAiRuntimeSettingsService : IAiRuntimeSettingsService
{
    public AiRuntimeSettingsSnapshot Snapshot { get; set; } = new();

    public AiRuntimeSettingsSnapshot GetSnapshot() => Snapshot;

    public Task<AdminAiSystemSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new AdminAiSystemSettingsResponse
        {
            UpdatedAt = Snapshot.UpdatedAt,
            UpdatedByUserId = Snapshot.UpdatedByUserId,
            Routing = new AdminAiRoutingSettingsDto
            {
                PrimaryTextProvider = Snapshot.Routing.PrimaryTextProvider,
                PrimaryVisionProvider = Snapshot.Routing.PrimaryVisionProvider,
                EnforceDailyTokenBudget = Snapshot.Routing.EnforceDailyTokenBudget,
                ApproxCharsPerToken = Snapshot.Routing.ApproxCharsPerToken,
                TextOutputTokenBudget = Snapshot.Routing.TextOutputTokenBudget,
                QuizOutputTokenBudget = Snapshot.Routing.QuizOutputTokenBudget,
                ImageOutputTokenBudget = Snapshot.Routing.ImageOutputTokenBudget,
                GeminiDailyTokenBudget = Snapshot.Routing.GeminiDailyTokenBudget,
                GroqDailyTokenBudget = Snapshot.Routing.GroqDailyTokenBudget,
                MinReservedTokensPerProvider = Snapshot.Routing.MinReservedTokensPerProvider,
                EnableProviderHealthSwitch = Snapshot.Routing.EnableProviderHealthSwitch,
                SlowRequestThresholdMs = Snapshot.Routing.SlowRequestThresholdMs,
                SlowRequestStreakThreshold = Snapshot.Routing.SlowRequestStreakThreshold,
                ConsecutiveFailureThreshold = Snapshot.Routing.ConsecutiveFailureThreshold,
                ProviderCooldownSeconds = Snapshot.Routing.ProviderCooldownSeconds,
                PreferFastestHealthyProvider = Snapshot.Routing.PreferFastestHealthyProvider,
                ProviderExecutionTimeoutMs = Snapshot.Routing.ProviderExecutionTimeoutMs
            },
            Gemini = new AdminGeminiSettingsDto
            {
                HasApiKey = !string.IsNullOrWhiteSpace(Snapshot.Gemini.ApiKey),
                BaseUrl = Snapshot.Gemini.BaseUrl,
                TextModel = Snapshot.Gemini.TextModel,
                VisionModel = Snapshot.Gemini.VisionModel,
                MaxInputCharacters = Snapshot.Gemini.MaxInputCharacters,
                MaxQuizInputCharacters = Snapshot.Gemini.MaxQuizInputCharacters,
                RequestTimeoutSeconds = Snapshot.Gemini.RequestTimeoutSeconds,
                MaxModelCandidates = Snapshot.Gemini.MaxModelCandidates,
                MaxRetriesPerModel = Snapshot.Gemini.MaxRetriesPerModel,
                FallbackModels = [.. Snapshot.Gemini.FallbackModels]
            },
            Groq = new AdminGroqSettingsDto
            {
                HasApiKey = !string.IsNullOrWhiteSpace(Snapshot.Groq.GroqApiKey),
                BaseUrl = Snapshot.Groq.BaseUrl,
                TextModel = Snapshot.Groq.TextModel,
                VisionModel = Snapshot.Groq.VisionModel,
                AudioModel = Snapshot.Groq.AudioModel,
                MaxInputCharacters = Snapshot.Groq.MaxInputCharacters,
                MaxQuizInputCharacters = Snapshot.Groq.MaxQuizInputCharacters,
                RequestTimeoutSeconds = Snapshot.Groq.RequestTimeoutSeconds,
                MaxModelCandidates = Snapshot.Groq.MaxModelCandidates,
                MaxConcurrentRequests = Snapshot.Groq.MaxConcurrentRequests,
                QueueWaitTimeoutSeconds = Snapshot.Groq.QueueWaitTimeoutSeconds,
                MaxRetriesPerModel = Snapshot.Groq.MaxRetriesPerModel,
                EnableResponseCache = Snapshot.Groq.EnableResponseCache,
                ResponseCacheDays = Snapshot.Groq.ResponseCacheDays,
                FallbackModels = [.. Snapshot.Groq.FallbackModels]
            }
        });
    }

    public Task<AdminAiSystemSettingsResponse> UpdateAdminSettingsAsync(
        AdminAiSystemSettingsUpdateRequest request,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Test double does not persist admin AI settings.");
    }
}

internal sealed class NoOpSummaryProcessingService : ISummaryProcessingService
{
    public Task<SummarizeUploadResponse> SummarizeUploadAsync(
        IFormFile file,
        int? userId,
        bool isGuest,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Summary upload flow is not used in this test.");
    }

    public Task<SummarizeUploadResponse> SummarizeTextAsync(
        string text,
        string? sourceHint,
        int? userId,
        bool isGuest,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Summary text flow is not used in this test.");
    }

    public Task<SummarizeUrlResponse> SummarizeFromUrlAsync(
        string url,
        int? userId,
        bool isGuest,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Summary URL flow is not used in this test.");
    }

    public Task<bool> GenerateApprovedContentSummaryAsync(
        int contentId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
