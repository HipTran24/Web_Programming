using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Services.AI;
using Web_Project.Services.Content;

namespace Web_Project.Tests.Summary;

public sealed class SummaryProcessingServiceTextTests
{
    [Fact]
    public async Task SummarizeTextAsync_ReturnsSummary_WhenInputIsValid()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService();
        var service = CreateService(groq, dbContext);

        var response = await service.SummarizeTextAsync("  Day la noi dung can tom tat.  ", "text", null, true, CancellationToken.None);

        var content = await dbContext.Contents.Include(x => x.AIProcess).SingleAsync();

        Assert.Equal(content.ContentId, response.ContentId);
        Assert.Equal("Tom tat tu fake groq.txt", response.FileName);
        Assert.Equal("text", response.InputType);
        Assert.Equal("Tom tat tu fake groq", response.Summary);
        Assert.Contains("Y chinh 1", response.KeyPoints);
        Assert.True(response.ExtractedTextLength > 0);
        Assert.Equal("text", groq.LastSourceHint);
        Assert.Equal("Day la noi dung can tom tat.", groq.LastText);
        Assert.Equal("FileUpload", content.SourceType);
        Assert.NotNull(content.AIProcess);
        Assert.Equal("Tom tat tu fake groq", content.AIProcess!.Summary);
    }

    [Fact]
    public async Task SummarizeTextAsync_GeneratesMeaningfulFileName_FromVietnameseSummary()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService
        {
            NextSummary = "Lịch sử Việt Nam và các mốc quan trọng"
        };
        var service = CreateService(groq, dbContext);

        var response = await service.SummarizeTextAsync("Noi dung lich su", "text", null, true, CancellationToken.None);

        Assert.Equal("Lịch sử Việt Nam và các mốc quan trọng.txt", response.FileName);
    }

    [Fact]
    public async Task SummarizeTextAsync_Throws_WhenInputIsEmpty()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService();
        var service = CreateService(groq, dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SummarizeTextAsync("   ", null, null, true, CancellationToken.None));
    }

    [Fact]
    public async Task SummarizeTextAsync_TrimsAndLimits_SourceHint()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService();
        var service = CreateService(groq, dbContext);
        var longHint = $"  {new string('v', 80)}  ";

        var response = await service.SummarizeTextAsync("Noi dung", longHint, null, true, CancellationToken.None);

        Assert.Equal(64, response.InputType.Length);
        Assert.Equal(response.InputType, groq.LastSourceHint);
    }

    [Fact]
    public async Task SummarizeTextAsync_FlagsSensitivePoliticalContent_ForAdminReview()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService
        {
            NextSummary = "Phân tích bầu cử và các hoạt động chính trị."
        };
        var service = CreateService(groq, dbContext);

        var response = await service.SummarizeTextAsync(
            "Đây là nội dung nói về bầu cử và chính trị hiện tại.",
            "text",
            userId: 9,
            isGuest: false,
            CancellationToken.None);

        Assert.True(response.RequiresAdminReview);
        Assert.Equal("Pending", response.ModerationStatus);
        Assert.NotEmpty(response.ModerationFlags);

        var moderation = await dbContext.ContentModerations.SingleAsync();
        Assert.Equal("Pending", moderation.Status);
        Assert.Contains("chính trị", moderation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SummarizeTextAsync_SplitsLongInput_IntoMultipleAiPasses()
    {
        await using var dbContext = CreateDbContext();
        var groq = new FakeGroqSummaryService
        {
            SummaryFactory = (_, _, callIndex) => new AiSummaryResult
            {
                Summary = $"Tom tat lan {callIndex}",
                KeyPoints = [$"Y chinh {callIndex}"]
            }
        };
        var service = CreateService(groq, dbContext);
        var longText = string.Join(
            "\n\n",
            Enumerable.Range(1, 36).Select(index =>
                $"Doan {index}: " +
                string.Join(" ", Enumerable.Repeat("Noi dung dai de kiem tra co che chia nho khi upload tai lieu.", 26))));

        var response = await service.SummarizeTextAsync(longText, "text", null, true, CancellationToken.None);

        Assert.True(groq.CallTexts.Count >= 3);
        Assert.True(groq.CallTexts[0].Length < longText.Length);
        Assert.Equal($"Tom tat lan {groq.CallTexts.Count}", response.Summary);
        Assert.Contains($"Y chinh {groq.CallTexts.Count}", response.KeyPoints);
    }

    private static SummaryProcessingService CreateService(FakeGroqSummaryService groq, AppDbContext dbContext)
    {
        return new SummaryProcessingService(
            groq,
            dbContext,
            new StubHttpClientFactory(),
            new KeywordContentSafetyService(),
            NullLogger<SummaryProcessingService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"summary-processing-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeGroqSummaryService : IGroqSummaryService
    {
        public string LastText { get; private set; } = string.Empty;

        public string LastSourceHint { get; private set; } = string.Empty;

        public List<string> CallTexts { get; } = [];

        public string NextSummary { get; set; } = "Tom tat tu fake groq";

        public Func<string, string, int, AiSummaryResult>? SummaryFactory { get; set; }

        public Task<AiSummaryResult> SummarizeTextAsync(string text, string sourceHint, CancellationToken cancellationToken)
        {
            LastText = text;
            LastSourceHint = sourceHint;
            CallTexts.Add(text);

            if (SummaryFactory is not null)
            {
                return Task.FromResult(SummaryFactory(text, sourceHint, CallTexts.Count));
            }

            return Task.FromResult(new AiSummaryResult
            {
                Summary = NextSummary,
                KeyPoints = ["Y chinh 1", "Y chinh 2"]
            });
        }

        public Task<AiSummaryResult> SummarizeImageAsync(byte[] imageBytes, string mimeType, string fileName, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiSummaryResult
            {
                Summary = "image",
                KeyPoints = []
            });
        }

        public Task<string> TranscribeAudioAsync(string audioFilePath, CancellationToken cancellationToken)
        {
            return Task.FromResult("audio transcript");
        }

        public Task<AiQuizResult> GenerateQuizAsync(string sourceText, int totalQuestions, string difficulty, string quizType, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiQuizResult
            {
                Questions = []
            });
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
