using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Services.AI;

namespace Web_Project.Tests.Summary;

public sealed class GeminiSummaryServiceTests
{
    [Fact]
    public async Task SummarizeTextAsync_Fallbacks_WhenSuccessResponseMissingContentParts()
    {
        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, """
                {"candidates":[{"finishReason":"SAFETY"}]}
                """),
            _ => JsonResponse(HttpStatusCode.OK, """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "{\"summary\":\"Tom tat\",\"keyPoints\":[\"Y1\"]}"
                          }
                        ]
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
    public async Task SummarizeTextAsync_ThrowsFriendlyMessage_WhenPromptFeedbackBlocksContent()
    {
        var blockedBody = """
            {
              "promptFeedback": {
                "blockReason": "SAFETY",
                "blockReasonMessage": "Blocked by safety policy"
              },
              "candidates": []
            }
            """;

        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.OK, blockedBody),
            _ => JsonResponse(HttpStatusCode.OK, blockedBody),
            _ => JsonResponse(HttpStatusCode.OK, blockedBody),
            _ => JsonResponse(HttpStatusCode.OK, blockedBody),
            _ => JsonResponse(HttpStatusCode.OK, blockedBody)
        );

        var service = CreateService(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SummarizeTextAsync("Noi dung", "text", CancellationToken.None));

        Assert.Contains("Gemini chặn nội dung đầu vào", ex.Message);
    }

    [Fact]
    public async Task SummarizeTextAsync_FallbacksToNextModel_WhenFirstModelReturns503()
    {
        var handler = new SequenceHttpMessageHandler(
            _ => JsonResponse(HttpStatusCode.ServiceUnavailable, """
                {
                  "error": {
                    "code": 503,
                    "status": "UNAVAILABLE",
                    "message": "This model is currently experiencing high demand. Please try again later."
                  }
                }
                """),
            _ => JsonResponse(HttpStatusCode.OK, """
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "{\"summary\":\"Da fallback\",\"keyPoints\":[\"Y2\"]}"
                          }
                        ]
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

    private static GeminiSummaryService CreateService(HttpMessageHandler handler)
    {
        var settings = new GeminiSettings
        {
            ApiKey = "test-key",
            BaseUrl = "https://generativelanguage.googleapis.com",
            TextModel = "gemini-a",
            VisionModel = "gemini-a",
            AudioModel = "gemini-a",
            MaxInputCharacters = 24000,
            FallbackModels = ["gemini-b"]
        };

        return new GeminiSummaryService(
            new HttpClient(handler),
            Options.Create(settings),
            NullLogger<GeminiSummaryService>.Instance);
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
