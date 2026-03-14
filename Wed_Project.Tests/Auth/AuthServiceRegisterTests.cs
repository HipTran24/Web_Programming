using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Web_Project.Models;
using Web_Project.Security;
using Web_Project.Services.Auth;
using Web_Project.Services.Otp;

namespace Web_Project.Tests.Auth;

public sealed class AuthServiceRegisterTests
{
    [Fact]
    public async Task RegisterAsync_ReturnsValidationErrors_WhenRequiredFieldsAreInvalid()
    {
        using var dbContext = CreateDbContext();
        var otpService = new FakeEmailOtpService();
        var service = CreateService(dbContext, otpService);

        var request = new RegisterRequest
        {
            Username = " ",
            FullName = " ",
            Email = "student@example.com",
            Password = "weak",
            ConfirmPassword = "weak",
            AcceptTerms = false
        };

        var result = await service.RegisterAsync(request, "127.0.0.1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(nameof(RegisterRequest.Username), result.ValidationErrors.Keys);
        Assert.Contains(nameof(RegisterRequest.FullName), result.ValidationErrors.Keys);
        Assert.Contains(nameof(RegisterRequest.AcceptTerms), result.ValidationErrors.Keys);
        Assert.Contains(nameof(RegisterRequest.Password), result.ValidationErrors.Keys);
        Assert.Empty(dbContext.Users);
        Assert.Equal(0, otpService.IssueCallCount);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsValidationError_WhenUsernameAlreadyExists()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, username: "existing.user", email: "existing@example.com");

        var otpService = new FakeEmailOtpService();
        var service = CreateService(dbContext, otpService);
        var request = CreateValidRequest();
        request.Username = "existing.user";
        request.Email = "new-user@example.com";

        var result = await service.RegisterAsync(request, "127.0.0.1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(nameof(RegisterRequest.Username), result.ValidationErrors.Keys);
        Assert.Equal(0, otpService.IssueCallCount);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsValidationError_WhenEmailAlreadyExists()
    {
        using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, username: "existing.user", email: "existing@example.com");

        var otpService = new FakeEmailOtpService();
        var service = CreateService(dbContext, otpService);
        var request = CreateValidRequest();
        request.Username = "new.user";
        request.Email = "EXISTING@example.com";

        var result = await service.RegisterAsync(request, "127.0.0.1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(nameof(RegisterRequest.Email), result.ValidationErrors.Keys);
        Assert.Equal(0, otpService.IssueCallCount);
    }

    [Fact]
    public async Task RegisterAsync_CreatesUserAndDispatchesOtp_WhenRequestIsValid()
    {
        using var dbContext = CreateDbContext();
        var otpService = new FakeEmailOtpService
        {
            NextDispatchResult = new OtpDispatchResult
            {
                Success = true,
                ExpiresAt = new DateTime(2030, 1, 1, 0, 5, 0, DateTimeKind.Utc)
            }
        };

        var service = CreateService(dbContext, otpService);
        var request = CreateValidRequest();
        request.Username = "  test.user  ";
        request.FullName = "  Nguyen Van A  ";
        request.Email = "  STUDENT@Example.com  ";

        var result = await service.RegisterAsync(request, "203.0.113.5", CancellationToken.None);

        Assert.True(result.Success);
        var response = Assert.IsType<RegisterResponse>(result.Response);
        Assert.Equal("test.user", response.Username);
        Assert.Equal("Nguyen Van A", response.FullName);
        Assert.Equal("student@example.com", response.Email);
        Assert.True(response.OtpDispatched);
        Assert.Equal(new DateTime(2030, 1, 1, 0, 5, 0, DateTimeKind.Utc), response.OtpExpiresAt);

        Assert.Equal(1, otpService.IssueCallCount);
        Assert.Equal("203.0.113.5", otpService.LastRequestIp);
        Assert.NotNull(otpService.LastIssuedUser);

        var savedUser = await dbContext.Users.SingleAsync();
        Assert.Equal("test.user", savedUser.Username);
        Assert.Equal("student@example.com", savedUser.Email);
        Assert.NotEqual(request.Password, savedUser.PasswordHash);
        Assert.StartsWith("PBKDF2$SHA256$", savedUser.PasswordHash, StringComparison.Ordinal);
        Assert.False(savedUser.IsEmailVerified);

        Assert.Single(dbContext.Roles.Where(x => x.RoleName == "User"));
    }

    [Fact]
    public async Task RegisterAsync_ReturnsSuccessWithWarningMessage_WhenOtpDispatchFails()
    {
        using var dbContext = CreateDbContext();
        var otpService = new FakeEmailOtpService
        {
            NextDispatchResult = new OtpDispatchResult
            {
                Success = false,
                Message = "smtp unavailable",
                ExpiresAt = null
            }
        };

        var service = CreateService(dbContext, otpService);

        var result = await service.RegisterAsync(CreateValidRequest(), "127.0.0.1", CancellationToken.None);

        Assert.True(result.Success);
        var response = Assert.IsType<RegisterResponse>(result.Response);
        Assert.False(response.OtpDispatched);
        Assert.Equal("Đăng ký thành công nhưng chưa gửi được OTP. Vui lòng gửi lại OTP.", response.Message);
        Assert.Equal(1, otpService.IssueCallCount);
    }

    private static AuthService CreateService(AppDbContext dbContext, FakeEmailOtpService otpService)
    {
        var jwtSettings = new JwtSettings
        {
            Issuer = "WedProject.Tests",
            Audience = "WedProject.Tests.Client",
            SecretKey = "this-is-a-very-strong-test-secret-key-12345",
            AccessTokenMinutes = 60,
            RememberMeAccessTokenDays = 7
        };

        var signingMaterial = JwtSigningMaterial.Create(jwtSettings, Directory.GetCurrentDirectory());

        return new AuthService(
            dbContext,
            otpService,
            Options.Create(jwtSettings),
            signingMaterial,
            NullLogger<AuthService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"register-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
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

    private static async Task SeedUserAsync(AppDbContext dbContext, string username, string email)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.RoleName == "User");
        if (role is null)
        {
            role = new Role { RoleName = "User" };
            dbContext.Roles.Add(role);
            await dbContext.SaveChangesAsync();
        }

        dbContext.Users.Add(new User
        {
            Username = username,
            FullName = "Existing User",
            Email = email,
            PasswordHash = "existing-hash",
            RoleId = role.RoleId,
            IsLocked = false,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeEmailOtpService : IEmailOtpService
    {
        public int IssueCallCount { get; private set; }

        public string LastRequestIp { get; private set; } = string.Empty;

        public User? LastIssuedUser { get; private set; }

        public OtpDispatchResult NextDispatchResult { get; set; } = new()
        {
            Success = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        public Task<OtpDispatchResult> IssueRegisterOtpAsync(User user, string requestIp, CancellationToken cancellationToken)
        {
            IssueCallCount++;
            LastIssuedUser = user;
            LastRequestIp = requestIp;
            return Task.FromResult(NextDispatchResult);
        }

        public Task<OtpVerificationResult> VerifyRegisterOtpAsync(string email, string otpCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OtpVerificationResult { Success = true });
        }

        public Task<OtpDispatchResult> ResendRegisterOtpAsync(string email, string requestIp, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OtpDispatchResult { Success = true });
        }
    }
}
