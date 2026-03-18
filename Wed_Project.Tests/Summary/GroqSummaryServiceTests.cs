using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Services.AI;

namespace Web_Project.Tests.Summary;

public sealed class GroqSummaryServiceTests
{
    [Fact]
    public async Task SummarizeTextAsync_Fallbacks_WhenSuccessResponseMissingContentParts()
    {
        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """
                {"id":"1","choices":[{"message":{}}]}
                """),
            _ => JsonResponse(HttpStatusCode.OK, """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"summary\":\"Tom tat\",\"keyPoints\":[\"Y1\"]}"
                      }
                    }
                  ]
                }
                """)
        );

        var service = CreateService(handler);

        var result = await service.SummarizeTextAsync("Noi dung can tom tat", "text", CancellationToken.None);

        Assert.Equal("Tom tat", result.Summary);
        Assert.Contains("Y1", result.KeyPoints);
        Assert.True(handler.RequestUris.Count >= 2);
    }

    [Fact]
    public async Task SummarizeTextAsync_ThrowsFriendlyMessage_WhenRateLimited()
    {
        var blockedBody = """
            {
              "error": {
                "message": "Rate limit reached. retry in 15s",
                "type": "rate_limit_exceeded",
                "code": 429
              },
              "choices": []
            }
            """;

        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.TooManyRequests, blockedBody)
        );

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SummarizeTextAsync("Noi dung", "text", CancellationToken.None));

        Assert.Contains("Groq Cloud", ex.Message);
    }

    [Fact]
    public async Task SummarizeTextAsync_FallbacksToNextModel_WhenFirstModelReturns503()
    {
        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.ServiceUnavailable, """
                {
                  "error": {
                    "code": 503,
                    "type": "service_unavailable",
                    "message": "This model is currently experiencing high demand. Please try again later."
                  }
                }
                """),
            _ => JsonResponse(HttpStatusCode.OK, """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"summary\":\"Da fallback\",\"keyPoints\":[\"Y2\"]}"
                      }
                    }
                  ]
                }
                """)
        );

        var service = CreateService(handler);

        var result = await service.SummarizeTextAsync("Noi dung", "text", CancellationToken.None);

        Assert.Equal("Da fallback", result.Summary);
        Assert.Contains("Y2", result.KeyPoints);
        Assert.True(handler.RequestUris.Count >= 2);
    }

    private static GroqSummaryService CreateService(HttpMessageHandler handler)
    {
        var settings = new GroqSettings
        {
            GroqApiKey = "test-key",
          BaseUrl = "https://api.groq.com/openai",
          TextModel = "llama-a",
          VisionModel = "llama-v",
          AudioModel = "whisper-a",
            MaxInputCharacters = 24000,
          FallbackModels = ["llama-b"]
        };

        return new GroqSummaryService(
            new HttpClient(handler),
            Options.Create(settings),
          new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GroqSummaryService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri ?? new Uri("https://invalid.local"));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No response configured for test handler.");
            }

            return Task.FromResult(_responses.Dequeue().Invoke(request));
        }
    }
}
