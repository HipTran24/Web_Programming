using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Web_Project.Controllers;
using Web_Project.Models;
using Web_Project.Services.Auth;
using Web_Project.Services.Otp;

namespace Web_Project.Tests.Auth;

public sealed class AuthControllerLoginCookieTests
{
    [Fact]
    public async Task Login_SetsAuthCookie_WhenServiceSucceeds()
    {
        var authService = new StubAuthService();
        var controller = CreateController(authService);

        var result = await controller.Login(
            new LoginRequest
            {
                EmailOrUsername = "admin",
                Password = "StrongPass1",
                RememberMe = true
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Contains("wedproject.auth=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_DeletesAuthCookie()
    {
        var controller = CreateController(new StubAuthService());

        var result = controller.Logout();

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains("wedproject.auth=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_ReturnsAvatarUrl_FromProfileMeta()
    {
        var controller = CreateController(new StubAuthService());
        var role = new Role
        {
            RoleId = 1,
            RoleName = "User"
        };
        var user = new User
        {
            UserId = 7,
            Username = "learner",
            FullName = "Learner Demo",
            Email = "learner@example.com",
            PasswordHash = "hash",
            RoleId = 1,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Email, "learner@example.com"),
            new Claim(ClaimTypes.Role, "User"),
        ], "TestAuth"));

        var dbContext = GetDbContext(controller);
        dbContext.Roles.Add(role);
        dbContext.Users.Add(user);
        dbContext.SystemSettings.Add(new SystemSetting
        {
            SettingKey = "user:7:profile-meta",
            SettingValue = "{\"avatarUrl\":\"data:image/png;base64,QUJD\"}",
            Description = "User profile meta",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = 7
        });
        await dbContext.SaveChangesAsync();

        var result = await controller.Me(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var avatarUrl = ok.Value?.GetType().GetProperty("avatarUrl")?.GetValue(ok.Value)?.ToString();
        Assert.Equal("data:image/png;base64,QUJD", avatarUrl);
    }

    private static AuthController CreateController(IAuthService authService)
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-controller-login-tests-{Guid.NewGuid()}")
            .Options;
        var dbContext = new AppDbContext(dbOptions);

        return new AuthController(authService, dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static AppDbContext GetDbContext(AuthController controller)
    {
        var field = typeof(AuthController).GetField("_dbContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return Assert.IsType<AppDbContext>(field?.GetValue(controller));
    }

    private sealed class StubAuthService : IAuthService
    {
        public Task<LoginServiceResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LoginServiceResult
            {
                Success = true,
                StatusCode = 200,
                Response = new LoginResponse
                {
                    UserId = 1,
                    Username = "admin",
                    FullName = "System Admin",
                    Email = "admin@example.com",
                    Role = "Admin",
                    IsAdmin = true,
                    AccessToken = "jwt-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                }
            });
        }

        public Task<LoginServiceResult> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<RegisterServiceResult> RegisterAsync(RegisterRequest request, string requestIp, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OtpVerificationResult> VerifyEmailOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OtpVerificationResult { Success = true });
        }

        public Task<OtpDispatchResult> ResendEmailOtpAsync(ResendEmailOtpRequest request, string requestIp, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OtpDispatchResult { Success = true });
        }
    }
}
