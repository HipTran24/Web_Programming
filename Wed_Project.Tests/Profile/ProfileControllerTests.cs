using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Controllers;
using Web_Project.Models.Dtos.User;
using Web_Project.Services.Users;

namespace Web_Project.Tests.Profile;

public sealed class ProfileControllerTests
{
    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenClaimMissing()
    {
        var controller = CreateController(new StubUserService(), null);

        var result = await controller.GetProfile(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WhenServiceSucceeds()
    {
        var service = new StubUserService
        {
            GetProfileResult = new UserProfileServiceResult
            {
                Success = true,
                Response = new ProfileResponse
                {
                    UserId = 7,
                    Username = "demo",
                    FullName = "Demo User",
                    Email = "demo@example.com",
                    Role = "User"
                }
            }
        };

        var controller = CreateController(service, 7);

        var result = await controller.GetProfile(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(7, payload.UserId);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenServiceFails()
    {
        var service = new StubUserService
        {
            UpdateProfileResult = new UserProfileServiceResult
            {
                Success = false,
                Message = "Email đã được sử dụng bởi tài khoản khác."
            }
        };

        var controller = CreateController(service, 1);

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest { FullName = "A B", Email = "a@example.com" },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = ReadAnonymousMessage(badRequest.Value);
        Assert.Contains("Email", message);
    }

    [Fact]
    public async Task NotificationEndpoints_ReturnOk_WhenServiceSucceeds()
    {
        var service = new StubUserService
        {
            GetNotifyResult = new UserNotificationSettingsServiceResult
            {
                Success = true,
                Response = new NotificationSettingsResponse
                {
                    NotifyReviewReminder = true,
                    NotifyQuizResult = false,
                    NotifyProductNews = true
                }
            },
            UpdateNotifyResult = new UserNotificationSettingsServiceResult
            {
                Success = true,
                Message = "ok",
                Response = new NotificationSettingsResponse
                {
                    NotifyReviewReminder = false,
                    NotifyQuizResult = true,
                    NotifyProductNews = false
                }
            }
        };

        var controller = CreateController(service, 1);

        var getResult = await controller.GetNotificationSettings(CancellationToken.None);
        Assert.IsType<OkObjectResult>(getResult.Result);

        var putResult = await controller.UpdateNotificationSettings(
            new NotificationSettingsRequest
            {
                NotifyReviewReminder = false,
                NotifyQuizResult = true,
                NotifyProductNews = false
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(putResult.Result);
    }

    private static ProfileController CreateController(IUserService service, int? userId)
    {
        var controller = new ProfileController(service);
        var context = new DefaultHttpContext();

        if (userId.HasValue)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                "TestAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    private static string ReadAnonymousMessage(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var property = value.GetType().GetProperty("message");
        return property?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private sealed class StubUserService : IUserService
    {
        public UserProfileServiceResult GetProfileResult { get; set; } = new() { Success = true, Response = new ProfileResponse() };
        public UserProfileServiceResult UpdateProfileResult { get; set; } = new() { Success = true, Response = new ProfileResponse() };
        public UserActionServiceResult ChangePasswordResult { get; set; } = new() { Success = true, Message = "ok" };
        public UserNotificationSettingsServiceResult GetNotifyResult { get; set; } = new() { Success = true, Response = new NotificationSettingsResponse() };
        public UserNotificationSettingsServiceResult UpdateNotifyResult { get; set; } = new() { Success = true, Response = new NotificationSettingsResponse() };
        public UserActionServiceResult DeleteResult { get; set; } = new() { Success = true, Message = "ok" };

        public Task<UserActionServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ChangePasswordResult);

        public Task<UserActionServiceResult> DeleteAccountAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(DeleteResult);

        public Task<UserNotificationSettingsServiceResult> GetNotificationSettingsAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(GetNotifyResult);

        public Task<UserProfileServiceResult> GetProfileAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult(GetProfileResult);

        public Task<UserNotificationSettingsServiceResult> UpdateNotificationSettingsAsync(int userId, NotificationSettingsRequest request, CancellationToken cancellationToken)
            => Task.FromResult(UpdateNotifyResult);

        public Task<UserProfileServiceResult> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken)
            => Task.FromResult(UpdateProfileResult);
    }
}
