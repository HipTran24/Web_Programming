using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Services.AI;

namespace Web_Project.Tests.Summary;

public sealed class MultiModelSummaryServiceTests
{
    [Fact]
    public async Task SummarizeTextAsync_DoesNotRejectRequest_BecauseOfInternalInputThreshold()
    {
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
            Options.Create(new GeminiSettings
            {
                ApiKey = "gemini-key",
                TextModel = "gemini-test",
                MaxInputCharacters = 4
            }),
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
            Options.Create(new GroqSettings
            {
                GroqApiKey = "groq-key",
                TextModel = "groq-test",
                MaxInputCharacters = 4
            }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GroqSummaryService>.Instance);

        var service = new MultiModelSummaryService(
            gemini,
            groq,
            Options.Create(new GeminiSettings
            {
                ApiKey = "gemini-key",
                TextModel = "gemini-test",
                MaxInputCharacters = 4
            }),
            Options.Create(new GroqSettings
            {
                GroqApiKey = "groq-key",
                TextModel = "groq-test",
                MaxInputCharacters = 4
            }),
            Options.Create(new AiRoutingSettings
            {
                PrimaryTextProvider = "gemini",
                PreferFastestHealthyProvider = false,
                EnforceDailyTokenBudget = false,
                TextOutputTokenBudget = 1500
            }),
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
