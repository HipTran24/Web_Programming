using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wed_Project.Controllers;
using Wed_Project.Models;
using Wed_Project.Services.Auth;
using Wed_Project.Services.Otp;

namespace Wed_Project.Tests.Auth;

public sealed class AuthControllerRegisterTests
{
    [Fact]
    public async Task Register_ReturnsValidationProblem_WhenModelStateIsInvalid()
    {
        var authService = new StubAuthService();
        var controller = CreateController(authService);
        controller.ModelState.AddModelError(nameof(RegisterRequest.Username), "Username is required");

        var actionResult = await controller.Register(CreateValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains(nameof(RegisterRequest.Username), problem.Errors.Keys);
        Assert.Equal(0, authService.RegisterCallCount);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenServiceReturnsConflict()
    {
        var authService = new StubAuthService
        {
            RegisterResult = new RegisterServiceResult
            {
                Success = false,
                IsConflict = true,
                Message = "Tên đăng nhập hoặc email đã tồn tại."
            }
        };
        var controller = CreateController(authService);

        var actionResult = await controller.Register(CreateValidRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("Tên đăng nhập hoặc email đã tồn tại.", ReadAnonymousMessage(conflict.Value));
        Assert.Equal("203.0.113.10", authService.LastRequestIp);
    }

    [Fact]
    public async Task Register_ReturnsValidationProblem_WhenServiceReturnsValidationErrors()
    {
        var authService = new StubAuthService
        {
            RegisterResult = new RegisterServiceResult
            {
                Success = false,
                ValidationErrors = new Dictionary<string, string[]>
                {
                    [nameof(RegisterRequest.Password)] = ["Mật khẩu yếu."]
                }
            }
        };
        var controller = CreateController(authService);

        var actionResult = await controller.Register(CreateValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains(nameof(RegisterRequest.Password), problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenServiceFailsWithoutConflictOrValidationErrors()
    {
        var authService = new StubAuthService
        {
            RegisterResult = new RegisterServiceResult
            {
                Success = false,
                Message = "Unexpected error"
            }
        };
        var controller = CreateController(authService);

        var actionResult = await controller.Register(CreateValidRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("Unexpected error", ReadAnonymousMessage(badRequest.Value));
    }

    [Fact]
    public async Task Register_ReturnsCreated_WhenServiceSucceeds()
    {
        var response = new RegisterResponse
        {
            UserId = 42,
            Username = "new.user",
            FullName = "New User",
            Email = "new.user@example.com",
            CreatedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsEmailVerified = false,
            OtpDispatched = true,
            OtpExpiresAt = new DateTime(2030, 1, 1, 0, 5, 0, DateTimeKind.Utc),
            Message = "ok"
        };

        var authService = new StubAuthService
        {
            RegisterResult = new RegisterServiceResult
            {
                Success = true,
                Response = response
            }
        };
        var controller = CreateController(authService);

        var actionResult = await controller.Register(CreateValidRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal("/api/auth/users/42", created.Location);
        var body = Assert.IsType<RegisterResponse>(created.Value);
        Assert.Equal(42, body.UserId);
        Assert.Equal("203.0.113.10", authService.LastRequestIp);
    }

    [Fact]
    public async Task Register_UsesEmptyRequestIp_WhenRemoteAddressIsUnavailable()
    {
        var authService = new StubAuthService
        {
            RegisterResult = new RegisterServiceResult
            {
                Success = true,
                Response = new RegisterResponse { UserId = 1, Username = "x", FullName = "x", Email = "x@example.com" }
            }
        };
        var controller = CreateController(authService, remoteIp: null);

        await controller.Register(CreateValidRequest(), CancellationToken.None);

        Assert.Equal(string.Empty, authService.LastRequestIp);
    }

    private static AuthController CreateController(StubAuthService authService, string? remoteIp = "203.0.113.10")
    {
        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }

        return new AuthController(authService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static RegisterRequest CreateValidRequest()
    {
        return new RegisterRequest
        {
            Username = "new.user",
            FullName = "New User",
            Email = "new.user@example.com",
            Password = "StrongPass1",
            ConfirmPassword = "StrongPass1",
            AcceptTerms = true
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

    private sealed class StubAuthService : IAuthService
    {
        public RegisterServiceResult RegisterResult { get; set; } = new()
        {
            Success = true,
            Response = new RegisterResponse { UserId = 1, Username = "new.user", FullName = "New User", Email = "new@example.com" }
        };

        public int RegisterCallCount { get; private set; }

        public string LastRequestIp { get; private set; } = string.Empty;

        public Task<RegisterServiceResult> RegisterAsync(RegisterRequest request, string requestIp, CancellationToken cancellationToken)
        {
            RegisterCallCount++;
            LastRequestIp = requestIp;
            return Task.FromResult(RegisterResult);
        }

        public Task<LoginServiceResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LoginServiceResult
            {
                Success = true,
                StatusCode = 200,
                Response = new LoginResponse
                {
                    UserId = 1,
                    Username = "new.user",
                    FullName = "New User",
                    Email = "new@example.com",
                    Role = "User",
                    AccessToken = "token"
                }
            });
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
