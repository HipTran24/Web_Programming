using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Models.Dtos.User;
using Web_Project.Security;
using Web_Project.Services.Users;

namespace Web_Project.Tests.Profile;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsProfileWithStats_WhenUserExists()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var result = await service.GetProfileAsync(1, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Equal("demo.user", result.Response!.Username);
        Assert.Equal("User", result.Response.Role);
        Assert.Equal(2, result.Response.TotalUploads);
        Assert.Equal(2, result.Response.TotalQuizAttempts);
        Assert.True(result.Response.AverageQuizScore > 0);
        Assert.Equal(2, result.Response.ActiveLearningDays);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesFullNameAndNormalizedEmail()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);
        const string avatarUrl = "data:image/png;base64,QUJDREVGRw==";

        var result = await service.UpdateProfileAsync(
            1,
            new UpdateProfileRequest
            {
                FullName = "  Nguyen Van Updated  ",
                Email = " UPDATED@EXAMPLE.COM ",
                Phone = " +84 912 345 678 ",
                Bio = "  Học đều mỗi ngày  ",
                AvatarUrl = avatarUrl
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Nguyen Van Updated", result.Response!.FullName);
        Assert.Equal("updated@example.com", result.Response.Email);
        Assert.Equal("+84 912 345 678", result.Response.Phone);
        Assert.Equal("Học đều mỗi ngày", result.Response.Bio);
        Assert.Equal(avatarUrl, result.Response.AvatarUrl);

        var readBack = await service.GetProfileAsync(1, CancellationToken.None);
        Assert.True(readBack.Success);
        Assert.Equal("+84 912 345 678", readBack.Response!.Phone);
        Assert.Equal("Học đều mỗi ngày", readBack.Response.Bio);
        Assert.Equal(avatarUrl, readBack.Response.AvatarUrl);
    }

    [Fact]
    public async Task UpdateProfileAsync_ReturnsFailure_WhenRequiredFieldsAreNull()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var result = await service.UpdateProfileAsync(
            1,
            new UpdateProfileRequest
            {
                FullName = null!,
                Email = null!,
                Phone = "0123",
                Bio = "demo"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Họ tên không được để trống.", result.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_FailsWhenCurrentPasswordIsWrong()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var result = await service.ChangePasswordAsync(
            1,
            new ChangePasswordRequest
            {
                CurrentPassword = "WrongPass1",
                NewPassword = "StrongerPass1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Mật khẩu hiện tại", result.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_UpdatesPasswordHash_WhenInputIsValid()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var result = await service.ChangePasswordAsync(
            1,
            new ChangePasswordRequest
            {
                CurrentPassword = "StrongPass1",
                NewPassword = "NewStrongPass1"
            },
            CancellationToken.None);

        Assert.True(result.Success);

        var user = await dbContext.Users.SingleAsync(x => x.UserId == 1);
        Assert.True(PasswordHashUtility.VerifyPassword("NewStrongPass1", user.PasswordHash));
    }

    [Fact]
    public async Task NotificationSettings_RoundTrip_Succeeds()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var update = await service.UpdateNotificationSettingsAsync(
            1,
            new NotificationSettingsRequest
            {
                NotifyReviewReminder = false,
                NotifyQuizResult = true,
                NotifyProductNews = false
            },
            CancellationToken.None);

        Assert.True(update.Success);

        var read = await service.GetNotificationSettingsAsync(1, CancellationToken.None);
        Assert.True(read.Success);
        Assert.False(read.Response.NotifyReviewReminder);
        Assert.True(read.Response.NotifyQuizResult);
        Assert.False(read.Response.NotifyProductNews);
    }

    [Fact]
    public async Task DeleteAccountAsync_SoftDeletesUser_InsteadOfRemovingRecord()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserDataAsync(dbContext);
        var service = new UserService(dbContext);

        var result = await service.DeleteAccountAsync(1, CancellationToken.None);

        Assert.True(result.Success);

        var hiddenUser = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleAsync(x => x.UserId == 1);

        Assert.False(hiddenUser.Status);
        Assert.True(hiddenUser.IsLocked);
        Assert.False(await dbContext.Users.AnyAsync(x => x.UserId == 1));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user-service-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedUserDataAsync(AppDbContext dbContext)
    {
        var role = new Role
        {
            RoleId = 1,
            RoleName = "User"
        };

        var user = new User
        {
            UserId = 1,
            Username = "demo.user",
            FullName = "Demo User",
            Email = "demo@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("StrongPass1"),
            RoleId = 1,
            IsLocked = false,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var content1 = new Content
        {
            ContentId = 1,
            UserId = 1,
            IsGuest = false,
            FileName = "Tai lieu 1",
            FileType = "text/plain",
            FilePath = "/tmp/doc-1.txt",
            SourceType = "FileUpload",
            ExtractedText = "abc",
            AI_DetectedSubject = "Toán",
            AI_DetectedGrade = "10",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var content2 = new Content
        {
            ContentId = 2,
            UserId = 1,
            IsGuest = false,
            FileName = "Tai lieu 2",
            FileType = "text/plain",
            FilePath = "/tmp/doc-2.txt",
            SourceType = "FileUpload",
            ExtractedText = "abc",
            AI_DetectedSubject = "Lý",
            AI_DetectedGrade = "10",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var quiz1 = new Quiz
        {
            QuizId = 1,
            ContentId = 1,
            UserId = 1,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "medium",
            QuizType = "multiple-choice",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var quiz2 = new Quiz
        {
            QuizId = 2,
            ContentId = 2,
            UserId = 1,
            IsGuest = false,
            TotalQuestions = 10,
            Difficulty = "easy",
            QuizType = "multiple-choice",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var attempt1 = new QuizAttempt
        {
            AttemptId = 1,
            QuizId = 1,
            UserId = 1,
            Score = 7.5,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            SubmittedAt = DateTime.UtcNow.AddDays(-2)
        };

        var attempt2 = new QuizAttempt
        {
            AttemptId = 2,
            QuizId = 2,
            UserId = 1,
            Score = 8.5,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        };

        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.Contents.AddRange(content1, content2);
        dbContext.Quizzes.AddRange(quiz1, quiz2);
        dbContext.QuizAttempts.AddRange(attempt1, attempt2);

        await dbContext.SaveChangesAsync();
    }
}
