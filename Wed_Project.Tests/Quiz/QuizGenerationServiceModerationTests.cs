using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Services.AI;
using Web_Project.Services.Quiz;

namespace Web_Project.Tests.QuizFeatures;

public sealed class QuizGenerationServiceModerationTests
{
    [Fact]
    public async Task GenerateQuizAsync_RejectsPendingModerationContent()
    {
        await using var dbContext = CreateDbContext();
        var role = new Role { RoleId = 1, RoleName = "User" };
        var user = new User
        {
            UserId = 15,
            Username = "learner",
            FullName = "Learner",
            Email = "learner@example.com",
            PasswordHash = "hashed",
            RoleId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var content = new Content
        {
            ContentId = 120,
            UserId = 15,
            IsGuest = false,
            FileName = "sensitive.txt",
            FileType = "txt",
            FilePath = "sensitive.txt",
            SourceType = "FileUpload",
            ExtractedText = "Nội dung có yếu tố nhạy cảm.",
            AI_DetectedSubject = "Xã hội",
            AI_DetectedGrade = string.Empty,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            AIProcess = new AIProcess
            {
                ProcessId = 220,
                Summary = "Tóm tắt nội dung.",
                KeyPoints = "[\"Y1\"]",
                ProcessingTime = 1.2,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.Contents.Add(content);
        dbContext.ContentModerations.Add(new ContentModeration
        {
            ModerationId = 320,
            ContentId = 120,
            Status = "Pending",
            Reason = "Nội dung đang chờ admin duyệt.",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        var service = new QuizGenerationService(dbContext, new FakeGroqSummaryService());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateQuizAsync(
            new GenerateQuizRequest
            {
                ContentId = 120,
                Difficulty = "medium",
                QuizType = "practice",
                TotalQuestions = 5,
                VariationNonce = "nonce-1"
            },
            userId: 15,
            requestIp: "127.0.0.1",
            userAgent: "tests",
            cancellationToken: CancellationToken.None));

        Assert.Contains("chờ admin duyệt", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"quiz-moderation-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeGroqSummaryService : IGroqSummaryService
    {
        public Task<AiSummaryResult> SummarizeTextAsync(string text, string sourceHint, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AiSummaryResult> SummarizeImageAsync(byte[] imageBytes, string mimeType, string fileName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> TranscribeAudioAsync(string audioFilePath, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AiQuizResult> GenerateQuizAsync(string sourceText, int totalQuestions, string difficulty, string quizType, CancellationToken cancellationToken)
            => Task.FromResult(new AiQuizResult { Questions = [] });
    }
}
