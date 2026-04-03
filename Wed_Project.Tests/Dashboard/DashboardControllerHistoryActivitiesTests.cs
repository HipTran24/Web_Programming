using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Project.Controllers;
using Web_Project.Models;

namespace Web_Project.Tests.Dashboard;

public sealed class DashboardControllerHistoryActivitiesTests
{
    [Fact]
    public async Task GetHistoryActivities_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, userId: null);

        var result = await controller.GetHistoryActivities("all", 1, 20, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetHistoryActivities_ReturnsPaginatedMixedFeed_ForAllFilter()
    {
        await using var dbContext = CreateDbContext();
        await SeedHistoryDataAsync(dbContext);
        var controller = CreateController(dbContext, userId: 100);

        var result = await controller.GetHistoryActivities("all", 1, 2, CancellationToken.None);

        var payload = ReadPayload(result);
        Assert.Equal("all", payload.GetProperty("filter").GetString());
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(5, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(4, payload.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, payload.GetProperty("totalPages").GetInt32());

        var items = payload.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, items.Count);

        var first = items[0];
        Assert.Equal("Quiz", first.GetProperty("kind").GetString());
        Assert.Equal("Ôn lại", first.GetProperty("actionText").GetString());
        Assert.Equal("/home/quiz-result.html", first.GetProperty("actionUrl").GetString());
        Assert.Equal("8.3/10", first.GetProperty("result").GetString());

        var second = items[1];
        Assert.Equal("Mở chi tiết", second.GetProperty("actionText").GetString());
        Assert.Equal("/home/content-detail.html?contentId=1", second.GetProperty("actionUrl").GetString());
    }

    [Fact]
    public async Task GetHistoryActivities_ReturnsOnlyContentItems_AndClampsPageInputs()
    {
        await using var dbContext = CreateDbContext();
        await SeedHistoryDataAsync(dbContext);
        var controller = CreateController(dbContext, userId: 100);

        var result = await controller.GetHistoryActivities("content", 0, 2, CancellationToken.None);

        var payload = ReadPayload(result);
        Assert.Equal("content", payload.GetProperty("filter").GetString());
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(5, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalItems").GetInt32());

        var items = payload.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal("Mở chi tiết", item.GetProperty("actionText").GetString());
            Assert.StartsWith("/home/content-detail.html?contentId=", item.GetProperty("actionUrl").GetString());
        });
    }

    [Fact]
    public async Task GetHistoryActivities_ReturnsEmptyItems_WhenRequestedPageExceedsAvailablePages()
    {
        await using var dbContext = CreateDbContext();
        await SeedHistoryDataAsync(dbContext);
        var controller = CreateController(dbContext, userId: 100);

        var result = await controller.GetHistoryActivities("quiz", 2, 5, CancellationToken.None);

        var payload = ReadPayload(result);
        Assert.Equal("quiz", payload.GetProperty("filter").GetString());
        Assert.Equal(2, payload.GetProperty("page").GetInt32());
        Assert.Equal(5, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, payload.GetProperty("totalPages").GetInt32());
        Assert.Empty(payload.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetHistoryActivities_DefaultsToAllFilter_WhenFilterIsUnknown()
    {
        await using var dbContext = CreateDbContext();
        await SeedHistoryDataAsync(dbContext);
        var controller = CreateController(dbContext, userId: 100);

        var result = await controller.GetHistoryActivities("unknown", 1, 20, CancellationToken.None);

        var payload = ReadPayload(result);
        Assert.Equal("all", payload.GetProperty("filter").GetString());
        Assert.Equal(4, payload.GetProperty("totalItems").GetInt32());
    }

    private static async Task SeedHistoryDataAsync(AppDbContext dbContext)
    {
        var now = new DateTime(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        var role = new Role
        {
            RoleId = 1,
            RoleName = "User"
        };

        var mainUser = new User
        {
            UserId = 100,
            Username = "main.user",
            FullName = "Main User",
            Email = "main.user@example.com",
            PasswordHash = "hashed",
            RoleId = role.RoleId,
            CreatedAt = now.AddDays(-20)
        };

        var otherUser = new User
        {
            UserId = 200,
            Username = "other.user",
            FullName = "Other User",
            Email = "other.user@example.com",
            PasswordHash = "hashed",
            RoleId = role.RoleId,
            CreatedAt = now.AddDays(-10)
        };

        var content1 = new Content
        {
            ContentId = 1,
            UserId = mainUser.UserId,
            IsGuest = false,
            FileName = "Đại số 10 - hàm số bậc hai",
            FileType = "text/plain",
            FilePath = "/tmp/c1.txt",
            SourceType = "FileUpload",
            ExtractedText = "content",
            AI_DetectedSubject = "Toán",
            AI_DetectedGrade = "10",
            CreatedAt = now.AddHours(-1)
        };

        var content2 = new Content
        {
            ContentId = 2,
            UserId = mainUser.UserId,
            IsGuest = false,
            FileName = "Vật lý 10 - chuyển động thẳng đều",
            FileType = "text/plain",
            FilePath = "/tmp/c2.txt",
            SourceType = "FileUpload",
            ExtractedText = "content",
            AI_DetectedSubject = "Vật lý",
            AI_DetectedGrade = "10",
            CreatedAt = now.AddHours(-2)
        };

        var guestContent = new Content
        {
            ContentId = 3,
            UserId = mainUser.UserId,
            IsGuest = true,
            FileName = "Guest content",
            FileType = "text/plain",
            FilePath = "/tmp/c3.txt",
            SourceType = "FileUpload",
            ExtractedText = "guest",
            AI_DetectedSubject = "Khác",
            AI_DetectedGrade = "10",
            CreatedAt = now.AddMinutes(-5)
        };

        var otherUserContent = new Content
        {
            ContentId = 4,
            UserId = otherUser.UserId,
            IsGuest = false,
            FileName = "Other user content",
            FileType = "text/plain",
            FilePath = "/tmp/c4.txt",
            SourceType = "FileUpload",
            ExtractedText = "other",
            AI_DetectedSubject = "Khác",
            AI_DetectedGrade = "11",
            CreatedAt = now.AddMinutes(-10)
        };

        var aiProcess = new AIProcess
        {
            ProcessId = 1,
            ContentId = content1.ContentId,
            Summary = "summary",
            KeyPoints = "kp",
            CreatedAt = now.AddMinutes(-50)
        };

        var quiz1 = new Quiz
        {
            QuizId = 1,
            ContentId = content1.ContentId,
            UserId = mainUser.UserId,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "medium",
            QuizType = "multiple-choice",
            CreatedAt = now.AddMinutes(-45)
        };

        var quiz2 = new Quiz
        {
            QuizId = 2,
            ContentId = content2.ContentId,
            UserId = mainUser.UserId,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "easy",
            QuizType = "multiple-choice",
            CreatedAt = now.AddHours(-3)
        };

        var quizOther = new Quiz
        {
            QuizId = 3,
            ContentId = otherUserContent.ContentId,
            UserId = otherUser.UserId,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "easy",
            QuizType = "multiple-choice",
            CreatedAt = now.AddMinutes(-8)
        };

        var attempt1 = new QuizAttempt
        {
            AttemptId = 1,
            QuizId = quiz1.QuizId,
            UserId = mainUser.UserId,
            Score = 8.26,
            StartedAt = now.AddMinutes(-35),
            SubmittedAt = now.AddMinutes(-30)
        };

        var attempt2 = new QuizAttempt
        {
            AttemptId = 2,
            QuizId = quiz2.QuizId,
            UserId = mainUser.UserId,
            Score = 6,
            StartedAt = now.AddHours(-3),
            SubmittedAt = now.AddHours(-2).AddMinutes(-30)
        };

        var attemptOther = new QuizAttempt
        {
            AttemptId = 3,
            QuizId = quizOther.QuizId,
            UserId = otherUser.UserId,
            Score = 9.5,
            StartedAt = now.AddMinutes(-7),
            SubmittedAt = now.AddMinutes(-6)
        };

        dbContext.Roles.Add(role);
        dbContext.Users.AddRange(mainUser, otherUser);
        dbContext.Contents.AddRange(content1, content2, guestContent, otherUserContent);
        dbContext.AIProcesses.Add(aiProcess);
        dbContext.Quizzes.AddRange(quiz1, quiz2, quizOther);
        dbContext.QuizAttempts.AddRange(attempt1, attempt2, attemptOther);

        await dbContext.SaveChangesAsync();
    }

    private static DashboardController CreateController(AppDbContext dbContext, int? userId)
    {
        var controller = new DashboardController(dbContext);
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                authenticationType: "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard-history-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static JsonElement ReadPayload(IActionResult actionResult)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(ok.Value);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        return document.RootElement.Clone();
    }
}
