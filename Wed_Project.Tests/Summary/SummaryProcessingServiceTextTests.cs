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
        var gemini = new FakeGeminiSummaryService();
        var service = CreateService(gemini, dbContext);

        var response = await service.SummarizeTextAsync("  Day la noi dung can tom tat.  ", "text", CancellationToken.None);

        var content = await dbContext.Contents.Include(x => x.AIProcess).SingleAsync();

        Assert.Equal(content.ContentId, response.ContentId);
        Assert.Equal("tom-tat-tu-fake-gemini.txt", response.FileName);
        Assert.Equal("text", response.InputType);
        Assert.Equal("Tom tat tu fake gemini", response.Summary);
        Assert.Contains("Y chinh 1", response.KeyPoints);
        Assert.True(response.ExtractedTextLength > 0);
        Assert.Equal("text", gemini.LastSourceHint);
        Assert.Equal("Day la noi dung can tom tat.", gemini.LastText);
        Assert.Equal("FileUpload", content.SourceType);
        Assert.NotNull(content.AIProcess);
        Assert.Equal("Tom tat tu fake gemini", content.AIProcess!.Summary);
    }

    [Fact]
    public async Task SummarizeTextAsync_GeneratesMeaningfulFileName_FromVietnameseSummary()
    {
        await using var dbContext = CreateDbContext();
        var gemini = new FakeGeminiSummaryService
        {
            NextSummary = "Lịch sử Việt Nam và các mốc quan trọng"
        };
        var service = CreateService(gemini, dbContext);

        var response = await service.SummarizeTextAsync("Noi dung lich su", "text", CancellationToken.None);

        Assert.Equal("lich-su-viet-nam-va-cac-moc-quan-trong.txt", response.FileName);
    }

    [Fact]
    public async Task SummarizeTextAsync_Throws_WhenInputIsEmpty()
    {
        await using var dbContext = CreateDbContext();
        var gemini = new FakeGeminiSummaryService();
        var service = CreateService(gemini, dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SummarizeTextAsync("   ", null, CancellationToken.None));
    }

    [Fact]
    public async Task SummarizeTextAsync_TrimsAndLimits_SourceHint()
    {
        await using var dbContext = CreateDbContext();
        var gemini = new FakeGeminiSummaryService();
        var service = CreateService(gemini, dbContext);
        var longHint = $"  {new string('v', 80)}  ";

        var response = await service.SummarizeTextAsync("Noi dung", longHint, CancellationToken.None);

        Assert.Equal(64, response.InputType.Length);
        Assert.Equal(response.InputType, gemini.LastSourceHint);
    }

    private static SummaryProcessingService CreateService(FakeGeminiSummaryService gemini, AppDbContext dbContext)
    {
        return new SummaryProcessingService(
            gemini,
            dbContext,
            new StubHttpClientFactory(),
            NullLogger<SummaryProcessingService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"summary-processing-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeGeminiSummaryService : IGeminiSummaryService
    {
        public string LastText { get; private set; } = string.Empty;

        public string LastSourceHint { get; private set; } = string.Empty;

        public string NextSummary { get; set; } = "Tom tat tu fake gemini";

        public Task<AiSummaryResult> SummarizeTextAsync(string text, string sourceHint, CancellationToken cancellationToken)
        {
            LastText = text;
            LastSourceHint = sourceHint;

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
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }
}
