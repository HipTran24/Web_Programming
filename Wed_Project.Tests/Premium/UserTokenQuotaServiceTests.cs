using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Services.Premium;

namespace Wed_Project.Tests.Premium;

public sealed class UserTokenQuotaServiceTests
{
    [Fact]
    public async Task NormalUser_IsBlockedAfterDaily200kLimit()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, isPremium: false);
        dbContext.DailyUsageCounters.Add(new DailyUsageCounter
        {
            UserId = user.UserId,
            UsageDate = DateTime.UtcNow.Date,
            TokenUsed = 199_900,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new UserTokenQuotaService(dbContext);

        var ex = await Assert.ThrowsAsync<TokenQuotaExceededException>(() =>
            service.EnsureCanConsumeAsync(user.UserId, 200, "Summary.Text", CancellationToken.None));

        Assert.Equal(200_000, ex.Limit);
    }

    [Fact]
    public async Task PremiumUser_CanUseMoreThanNormalLimitButIsBlockedAfter500k()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, isPremium: true);

        var service = new UserTokenQuotaService(dbContext);
        await service.EnsureCanConsumeAsync(user.UserId, 250_000, "Summary.Text", CancellationToken.None);

        var status = await service.GetStatusAsync(user.UserId, CancellationToken.None);
        Assert.True(status.IsPremium);
        Assert.Equal(500_000, status.DailyTokenLimit);
        Assert.Equal(250_000, status.TokenUsedToday);

        var ex = await Assert.ThrowsAsync<TokenQuotaExceededException>(() =>
            service.EnsureCanConsumeAsync(user.UserId, 250_001, "Quiz.Generate", CancellationToken.None));

        Assert.Equal(500_000, ex.Limit);
    }

    [Fact]
    public async Task UsageResetsByDate()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, isPremium: false);
        dbContext.DailyUsageCounters.Add(new DailyUsageCounter
        {
            UserId = user.UserId,
            UsageDate = DateTime.UtcNow.Date.AddDays(-1),
            TokenUsed = 200_000,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        var service = new UserTokenQuotaService(dbContext);
        await service.EnsureCanConsumeAsync(user.UserId, 1_000, "Summary.Text", CancellationToken.None);

        var status = await service.GetStatusAsync(user.UserId, CancellationToken.None);
        Assert.Equal(200_000, status.DailyTokenLimit);
        Assert.Equal(1_000, status.TokenUsedToday);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"premium-quota-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<User> SeedUserAsync(AppDbContext dbContext, bool isPremium)
    {
        var role = new Role { RoleName = "User" };
        var user = new User
        {
            Username = Guid.NewGuid().ToString("N")[..12],
            FullName = "Premium Test User",
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = role,
            Status = true,
            IsEmailVerified = true,
            IsPremium = isPremium,
            SubscriptionTier = isPremium ? "Premium" : "Normal",
            PremiumStartedAt = isPremium ? DateTime.UtcNow : null,
            PremiumExpiresAt = isPremium ? DateTime.UtcNow.AddDays(30) : null,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
