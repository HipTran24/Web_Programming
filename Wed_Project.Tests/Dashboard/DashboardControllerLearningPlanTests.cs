using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Project.Controllers;
using Web_Project.Models;

namespace Web_Project.Tests.Dashboard;

public sealed class DashboardControllerLearningPlanTests
{
    [Fact]
    public async Task GetLearningPlan_ReturnsUnauthorized_WhenUserIdClaimIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext, userId: null);

        var result = await controller.GetLearningPlan(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetLearningPlan_ReturnsMappedPayload_WithFocusTopicMetrics()
    {
        await using var dbContext = CreateDbContext();
        await SeedLearningPlanDataAsync(dbContext);
        var controller = CreateController(dbContext, userId: 100);

        var result = await controller.GetLearningPlan(CancellationToken.None);

        var payload = ReadPayload(result);

        Assert.Equal(7, payload.GetProperty("weeklyGoalSessions").GetInt32());
        Assert.True(payload.GetProperty("weekProgressPercent").GetInt32() >= 0);

        var focusTopics = payload.GetProperty("focusTopics").EnumerateArray().ToList();
        Assert.NotEmpty(focusTopics);

        var firstTopic = focusTopics[0];
        Assert.Equal("Toán", firstTopic.GetProperty("name").GetString());
        Assert.True(firstTopic.GetProperty("accuracyPercent").GetInt32() >= 0);
        Assert.True(firstTopic.GetProperty("attempts").GetInt32() >= 1);

        var tasks = payload.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, task =>
        {
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("title").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("actionUrl").GetString()));
        });
    }

    private static async Task SeedLearningPlanDataAsync(AppDbContext dbContext)
    {
        var now = new DateTime(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        var role = new Role
        {
            RoleId = 1,
            RoleName = "User"
        };

        var user = new User
        {
            UserId = 100,
            Username = "learner.user",
            FullName = "Learner User",
            Email = "learner.user@example.com",
            PasswordHash = "hashed",
            RoleId = role.RoleId,
            CreatedAt = now.AddDays(-20)
        };

        var content = new Content
        {
            ContentId = 1,
            UserId = user.UserId,
            IsGuest = false,
            FileName = "Đại số 10",
            FileType = "text/plain",
            FilePath = "/tmp/c1.txt",
            SourceType = "FileUpload",
            ExtractedText = "content",
            AI_DetectedSubject = "Toán",
            AI_DetectedGrade = "10",
            CreatedAt = now.AddDays(-1)
        };

        var quiz = new Quiz
        {
            QuizId = 1,
            ContentId = content.ContentId,
            UserId = user.UserId,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "medium",
            QuizType = "multiple-choice",
            CreatedAt = now.AddHours(-20)
        };

        var attempt1 = new QuizAttempt
        {
            AttemptId = 1,
            QuizId = quiz.QuizId,
            UserId = user.UserId,
            Score = 6.5,
            StartedAt = now.AddHours(-18),
            SubmittedAt = now.AddHours(-17)
        };

        var attempt2 = new QuizAttempt
        {
            AttemptId = 2,
            QuizId = quiz.QuizId,
            UserId = user.UserId,
            Score = 7.2,
            StartedAt = now.AddHours(-10),
            SubmittedAt = now.AddHours(-9)
        };

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.Contents.Add(content);
        dbContext.Quizzes.Add(quiz);
        dbContext.QuizAttempts.AddRange(attempt1, attempt2);

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
            .UseInMemoryDatabase($"dashboard-learning-plan-tests-{Guid.NewGuid()}")
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
