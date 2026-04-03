using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Security;
using Web_Project.Services.Auth;
using Web_Project.Services.Otp;

namespace Web_Project.Tests.Auth;

public sealed class AuthServiceLoginTests
{
    [Fact]
    public async Task LoginAsync_ReturnsValidationErrors_WhenFieldsAreNull()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.LoginAsync(
            new LoginRequest
            {
                EmailOrUsername = null!,
                Password = null!
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(nameof(LoginRequest.EmailOrUsername), result.ValidationErrors.Keys);
        Assert.Contains(nameof(LoginRequest.Password), result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task LoginAsync_Fails_WhenUserWasSoftDeleted()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext);
        dbContext.Users.Add(new User
        {
            UserId = 2,
            Username = "deleted.user",
            FullName = "Deleted User",
            Email = "deleted@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("StrongPass1"),
            RoleId = 1,
            Status = false,
            IsLocked = false,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.LoginAsync(
            new LoginRequest
            {
                EmailOrUsername = "deleted.user",
                Password = "StrongPass1"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    private static AuthService CreateService(AppDbContext dbContext)
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
            new FakeEmailOtpService(),
            Options.Create(new GoogleAuthSettings { ClientId = "test-client-id" }),
            Options.Create(jwtSettings),
            signingMaterial,
            NullLogger<AuthService>.Instance);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"login-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedUserAsync(AppDbContext dbContext)
    {
        dbContext.Roles.Add(new Role
        {
            RoleId = 1,
            RoleName = "User"
        });

        dbContext.Users.Add(new User
        {
            UserId = 1,
            Username = "demo.user",
            FullName = "Demo User",
            Email = "demo@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("StrongPass1"),
            RoleId = 1,
            IsLocked = false,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeEmailOtpService : IEmailOtpService
    {
        public Task<OtpDispatchResult> IssueRegisterOtpAsync(User user, string requestIp, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OtpDispatchResult { Success = true });
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
