using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Project.Controllers;
using Web_Project.Models;
using Web_Project.Models.Dtos.User;
using Web_Project.Security;

namespace Web_Project.Tests.Admin;

public sealed class AdminControllerProfileAndOverviewTests
{
    [Fact]
    public async Task GetOverview_ReturnsAggregatedCounts_FromDatabaseRecords()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.GetOverview(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var kpis = document.RootElement.GetProperty("kpis");

        Assert.Equal(2, kpis.GetProperty("totalUsers").GetInt32());
        Assert.Equal(1, kpis.GetProperty("totalAdmins").GetInt32());
        Assert.Equal(1, kpis.GetProperty("lockedUsers").GetInt32());
        Assert.Equal(2, kpis.GetProperty("verifiedUsers").GetInt32());
        Assert.Equal(2, kpis.GetProperty("totalContents").GetInt32());
        Assert.Equal(2, kpis.GetProperty("totalQuizzes").GetInt32());
        Assert.Equal(1, kpis.GetProperty("pendingModeration").GetInt32());
        Assert.Equal(2, kpis.GetProperty("aiTotal24h").GetInt32());
        Assert.Equal(1, kpis.GetProperty("aiErrors24h").GetInt32());
        Assert.Equal(2, kpis.GetProperty("activeUsers7Days").GetInt32());
    }

    [Fact]
    public async Task GetProfile_ReturnsRealProfileAndAdminStats()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.GetProfile(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var profile = document.RootElement.GetProperty("profile");
        var adminStats = document.RootElement.GetProperty("adminStats");

        Assert.Equal("System Admin", profile.GetProperty("FullName").GetString());
        Assert.Equal("admin@local", profile.GetProperty("Email").GetString());
        Assert.Equal("0987654321", profile.GetProperty("phone").GetString());
        Assert.Equal("Giám sát hệ thống học tập.", profile.GetProperty("bio").GetString());
        Assert.Equal(1, profile.GetProperty("totalUploads").GetInt32());
        Assert.Equal(1, profile.GetProperty("totalQuizAttempts").GetInt32());
        Assert.Equal(3, adminStats.GetProperty("TotalAuditActions").GetInt32());
        Assert.Equal(2, adminStats.GetProperty("ManagedUsers").GetInt32());
        Assert.Equal(2, adminStats.GetProperty("ReviewedContents").GetInt32());
        Assert.Equal(1, adminStats.GetProperty("CreatedAdmins").GetInt32());
    }

    [Fact]
    public async Task GetAiLogs_ReturnsLatencyAndErrorAnalytics()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.GetAiLogs(errorsOnly: false, page: 1, pageSize: 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var summary = document.RootElement.GetProperty("summary");
        var actionBreakdown = document.RootElement.GetProperty("actionBreakdown");
        var latencyTrend = document.RootElement.GetProperty("latencyTrend");

        Assert.Equal(2, summary.GetProperty("totalLogs24h").GetInt32());
        Assert.Equal(1, summary.GetProperty("errors24h").GetInt32());
        Assert.True(summary.GetProperty("avgLatencyMs24h").GetDouble() > 0);
        Assert.True(actionBreakdown.GetArrayLength() >= 1);
        Assert.Equal(7, latencyTrend.GetArrayLength());
    }

    [Fact]
    public async Task UpdateProfile_PersistsChanges_AndWritesAuditLog()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest
            {
                FullName = "Admin Updated",
                Email = "admin.updated@example.com",
                Phone = "0900000000",
                Bio = "Cập nhật hồ sơ quản trị."
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("Cập nhật hồ sơ quản trị thành công.", document.RootElement.GetProperty("message").GetString());

        var admin = await dbContext.Users.SingleAsync(x => x.UserId == 7);
        Assert.Equal("Admin Updated", admin.FullName);
        Assert.Equal("admin.updated@example.com", admin.Email);

        var profileMeta = await dbContext.SystemSettings.SingleAsync(x => x.SettingKey == "user:7:profile-meta");
        using (var metaDocument = JsonDocument.Parse(profileMeta.SettingValue))
        {
            Assert.Equal("0900000000", metaDocument.RootElement.GetProperty("Phone").GetString());
            Assert.Equal("Cập nhật hồ sơ quản trị.", metaDocument.RootElement.GetProperty("Bio").GetString());
        }

        var audit = await dbContext.AdminAuditLogs.OrderByDescending(x => x.AuditId).FirstAsync();
        Assert.Equal("UpdateProfile", audit.ActionType);
        Assert.Equal("7", audit.TargetId);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenRequiredFieldsAreBlank()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest
            {
                FullName = "   ",
                Email = "   ",
                Phone = "0900000000",
                Bio = "demo"
            },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("Họ tên không được để trống.", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ChangeProfilePassword_UpdatesPasswordHash_AndWritesAuditLog()
    {
        await using var dbContext = CreateDbContext();
        await SeedAdminScenarioAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.ChangeProfilePassword(
            new ChangePasswordRequest
            {
                CurrentPassword = "Admin123!",
                NewPassword = "BetterPass9"
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var message = ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString();
        Assert.Equal("Đổi mật khẩu quản trị thành công.", message);

        var admin = await dbContext.Users.SingleAsync(x => x.UserId == 7);
        Assert.True(PasswordHashUtility.VerifyPassword("BetterPass9", admin.PasswordHash));

        var audit = await dbContext.AdminAuditLogs.OrderByDescending(x => x.AuditId).FirstAsync();
        Assert.Equal("ChangePassword", audit.ActionType);
        Assert.Equal("7", audit.TargetId);
    }

    private static async Task SeedAdminScenarioAsync(AppDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        var adminRole = new Role { RoleId = 1, RoleName = "Admin" };
        var userRole = new Role { RoleId = 2, RoleName = "User" };

        var admin = new User
        {
            UserId = 7,
            Username = "admin",
            FullName = "System Admin",
            Email = "admin@local",
            PasswordHash = PasswordHashUtility.HashPassword("Admin123!"),
            RoleId = 1,
            IsLocked = false,
            IsEmailVerified = true,
            CreatedAt = now.AddDays(-30)
        };

        var user = new User
        {
            UserId = 8,
            Username = "learner",
            FullName = "Learner One",
            Email = "learner@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("UserPass9"),
            RoleId = 2,
            IsLocked = true,
            IsEmailVerified = true,
            CreatedAt = now.AddDays(-7)
        };

        var adminContent = new Content
        {
            ContentId = 100,
            UserId = 7,
            IsGuest = false,
            FileName = "admin-guide.pdf",
            FileType = "application/pdf",
            FilePath = "/tmp/admin-guide.pdf",
            SourceType = "FileUpload",
            ExtractedText = "admin content",
            AI_DetectedSubject = "Quản trị",
            AI_DetectedGrade = "N/A",
            CreatedAt = now.AddDays(-2)
        };

        var userContent = new Content
        {
            ContentId = 101,
            UserId = 8,
            IsGuest = false,
            FileName = "learner-notes.pdf",
            FileType = "application/pdf",
            FilePath = "/tmp/learner-notes.pdf",
            SourceType = "FileUpload",
            ExtractedText = "learner content",
            AI_DetectedSubject = "Toán",
            AI_DetectedGrade = "12",
            CreatedAt = now.AddDays(-1)
        };

        var adminQuiz = new Quiz
        {
            QuizId = 200,
            ContentId = 100,
            UserId = 7,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "Medium",
            QuizType = "Practice",
            CreatedAt = now.AddDays(-2)
        };

        var userQuiz = new Quiz
        {
            QuizId = 201,
            ContentId = 101,
            UserId = 8,
            IsGuest = false,
            TotalQuestions = 8,
            Difficulty = "Easy",
            QuizType = "Practice",
            CreatedAt = now.AddDays(-1)
        };

        dbContext.Roles.AddRange(adminRole, userRole);
        dbContext.Users.AddRange(admin, user);
        dbContext.Contents.AddRange(adminContent, userContent);
        dbContext.Quizzes.AddRange(adminQuiz, userQuiz);
        dbContext.QuizAttempts.AddRange(
            new QuizAttempt
            {
                AttemptId = 300,
                QuizId = 200,
                UserId = 7,
                Score = 9.5,
                StartedAt = now.AddDays(-2),
                SubmittedAt = now.AddDays(-2)
            },
            new QuizAttempt
            {
                AttemptId = 301,
                QuizId = 201,
                UserId = 8,
                Score = 7.5,
                StartedAt = now.AddDays(-1),
                SubmittedAt = now.AddDays(-1)
            });
        dbContext.ContentModerations.AddRange(
            new ContentModeration
            {
                ModerationId = 400,
                ContentId = 100,
                Status = "Approved",
                Reason = "Đạt chuẩn",
                ReviewedByUserId = 7,
                CreatedAt = now.AddDays(-2),
                UpdatedAt = now.AddDays(-2),
                ReviewedAt = now.AddDays(-2)
            },
            new ContentModeration
            {
                ModerationId = 401,
                ContentId = 101,
                Status = "Pending",
                Reason = "Chờ xử lý",
                ReviewedByUserId = 7,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1),
                ReviewedAt = now.AddDays(-1)
            });
        dbContext.AISystemLogs.AddRange(
            new AISystemLog
            {
                LogId = 500,
                ActionType = "Summary",
                UserId = 7,
                IsGuest = false,
                ProcessingTime = 1.2,
                IsError = false,
                CreatedAt = now.AddHours(-4)
            },
            new AISystemLog
            {
                LogId = 501,
                ActionType = "QuizGeneration",
                UserId = 8,
                IsGuest = false,
                ProcessingTime = 2.3,
                IsError = true,
                CreatedAt = now.AddHours(-3)
            });
        dbContext.DailyUsageCounters.AddRange(
            new DailyUsageCounter
            {
                CounterId = 600,
                UsageDate = now.Date.AddDays(-1),
                UserId = 7,
                UploadCount = 1,
                AIProcessCount = 2,
                QuizGenerationCount = 1,
                TotalProcessingTime = 3.4,
                UpdatedAt = now.AddHours(-2)
            },
            new DailyUsageCounter
            {
                CounterId = 601,
                UsageDate = now.Date,
                UserId = 8,
                UploadCount = 1,
                AIProcessCount = 1,
                QuizGenerationCount = 1,
                TotalProcessingTime = 2.1,
                UpdatedAt = now.AddHours(-1)
            });
        dbContext.AdminAuditLogs.AddRange(
            new AdminAuditLog
            {
                AuditId = 700,
                AdminUserId = 7,
                ActionType = "CreateAdmin",
                TargetType = "User",
                TargetId = "9",
                DetailJson = "{}",
                IpAddress = "127.0.0.1",
                CreatedAt = now.AddDays(-5)
            },
            new AdminAuditLog
            {
                AuditId = 701,
                AdminUserId = 7,
                ActionType = "UpdateUser",
                TargetType = "User",
                TargetId = "8",
                DetailJson = "{}",
                IpAddress = "127.0.0.1",
                CreatedAt = now.AddDays(-2)
            },
            new AdminAuditLog
            {
                AuditId = 702,
                AdminUserId = 7,
                ActionType = "ContentModeration",
                TargetType = "Content",
                TargetId = "101",
                DetailJson = "{}",
                IpAddress = "127.0.0.1",
                CreatedAt = now.AddHours(-1)
            });
        dbContext.SystemSettings.Add(new SystemSetting
        {
            SettingId = 800,
            SettingKey = "user:7:profile-meta",
            SettingValue = "{\"Phone\":\"0987654321\",\"Bio\":\"Giám sát hệ thống học tập.\"}",
            Description = "User profile meta",
            UpdatedByUserId = 7,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();
    }

    private static AdminController CreateController(AppDbContext dbContext, int? userId, string role)
    {
        var controller = new AdminController(dbContext);
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                    new Claim(ClaimTypes.Role, role)
                ],
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
            .UseInMemoryDatabase($"admin-controller-profile-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
