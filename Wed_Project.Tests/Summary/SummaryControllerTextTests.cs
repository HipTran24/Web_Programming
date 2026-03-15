using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Web_Project.Controllers;
using Web_Project.Models;
using Web_Project.Services.Content;

namespace Web_Project.Tests.Summary;

public sealed class SummaryControllerTextTests
{
    [Fact]
    public async Task SummarizeText_ReturnsValidationProblem_WhenModelStateIsInvalid()
    {
        var service = new StubSummaryProcessingService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(SummarizeTextRequest.Text), "Text is required");

        var actionResult = await controller.SummarizeText(new SummarizeTextRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains(nameof(SummarizeTextRequest.Text), problem.Errors.Keys);
        Assert.Equal(0, service.TextCallCount);
    }

    [Fact]
    public async Task SummarizeText_ReturnsOk_WhenServiceSucceeds()
    {
        var service = new StubSummaryProcessingService
        {
            TextResult = new SummarizeUploadResponse
            {
                FileName = "inline-text.txt",
                InputType = "text",
                ExtractedTextLength = 25,
                Summary = "Tom tat",
                KeyPoints = ["Y chinh 1", "Y chinh 2"],
                Preview = "Preview"
            }
        };

        var controller = CreateController(service);
        var request = new SummarizeTextRequest
        {
            Text = "Noi dung can tom tat",
            SourceHint = "text"
        };

        var actionResult = await controller.SummarizeText(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var body = Assert.IsType<SummarizeUploadResponse>(ok.Value);
        Assert.Equal("Tom tat", body.Summary);
        Assert.Equal("Noi dung can tom tat", service.LastText);
        Assert.Equal("text", service.LastSourceHint);
        Assert.Equal(1, service.TextCallCount);
    }

    [Fact]
    public async Task SummarizeText_ReturnsBadRequest_WhenServiceThrowsInvalidOperation()
    {
        var service = new StubSummaryProcessingService
        {
            TextException = new InvalidOperationException("Noi dung rong")
        };
        var controller = CreateController(service);

        var actionResult = await controller.SummarizeText(
            new SummarizeTextRequest { Text = "   " },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("Noi dung rong", ReadAnonymousMessage(badRequest.Value));
    }

    private static SummaryController CreateController(StubSummaryProcessingService service)
    {
        return new SummaryController(service, NullLogger<SummaryController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static string? ReadAnonymousMessage(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var property = value.GetType().GetProperty("message");
        return property?.GetValue(value)?.ToString();
    }

    private sealed class StubSummaryProcessingService : ISummaryProcessingService
    {
        public int TextCallCount { get; private set; }

        public string LastText { get; private set; } = string.Empty;

        public string? LastSourceHint { get; private set; }

        public Exception? TextException { get; set; }

        public SummarizeUploadResponse TextResult { get; set; } = new()
        {
            FileName = "inline-text.txt",
            InputType = "text",
            ExtractedTextLength = 10,
            Summary = "ok"
        };

        public Task<SummarizeUploadResponse> SummarizeUploadAsync(IFormFile file, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SummarizeUploadResponse());
        }

        public Task<SummarizeUploadResponse> SummarizeTextAsync(string text, string? sourceHint, CancellationToken cancellationToken)
        {
            TextCallCount++;
            LastText = text;
            LastSourceHint = sourceHint;

            if (TextException is not null)
            {
                throw TextException;
            }

            return Task.FromResult(TextResult);
        }

        public Task<SummarizeUrlResponse> SummarizeFromUrlAsync(string url, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SummarizeUrlResponse());
        }
    }
}
