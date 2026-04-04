using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Web_Project.Models;
using Web_Project.Services.AI;
using Web_Project.Tests.TestDoubles;

namespace Web_Project.Tests.Summary;

public sealed class MultiModelSummaryServiceTests
{
    [Fact]
    public async Task SummarizeTextAsync_DoesNotRejectRequest_BecauseOfInternalInputThreshold()
    {
        var runtimeSettings = new FakeAiRuntimeSettingsService
        {
            Snapshot = new AiRuntimeSettingsSnapshot
            {
                Gemini = new GeminiSettings
                {
                    ApiKey = "gemini-key",
                    TextModel = "gemini-test",
                    VisionModel = "gemini-vision-test",
                    MaxInputCharacters = 4
                },
                Groq = new GroqSettings
                {
                    GroqApiKey = "groq-key",
                    TextModel = "groq-test",
                    VisionModel = "groq-vision-test",
                    AudioModel = "groq-audio-test",
                    MaxInputCharacters = 4
                },
                Routing = new AiRoutingSettings
                {
                    PrimaryTextProvider = "gemini",
                    PreferFastestHealthyProvider = false,
                    EnforceDailyTokenBudget = false,
                    TextOutputTokenBudget = 1500
                }
            }
        };

        var gemini = new GeminiSummaryService(
            new HttpClient(new StaticJsonHandler(
                HttpStatusCode.OK,
                """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "{\"summary\":\"Da xu ly\",\"keyPoints\":[\"Y1\",\"Y2\"]}"
                          }
                        ]
                      }
                    }
                  ]
                }
                """)),
            runtimeSettings,
            NullLogger<GeminiSummaryService>.Instance);

        var groq = new GroqSummaryService(
            new HttpClient(new StaticJsonHandler(
                HttpStatusCode.OK,
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"summary\":\"Da xu ly groq\",\"keyPoints\":[\"Y1\"]}"
                      }
                    }
                  ]
                }
                """)),
            runtimeSettings,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GroqSummaryService>.Instance);

        var service = new MultiModelSummaryService(
            gemini,
            groq,
            runtimeSettings,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MultiModelSummaryService>.Instance);

        var result = await service.SummarizeTextAsync(
            "Noi dung upload dai hon muc limit noi bo cu.",
            "text",
            CancellationToken.None);

        Assert.Equal("Da xu ly", result.Summary);
        Assert.Contains("Y1", result.KeyPoints);
    }

    private sealed class StaticJsonHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
