using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Project.Controllers;
using Web_Project.Models;
using Web_Project.Security;
using Web_Project.Tests.TestDoubles;

namespace Web_Project.Tests.Admin;

public sealed class AdminControllerAdminAccountTests
{
    [Fact]
    public async Task CreateAdminUser_ReturnsCreated_AndPersistsAdminAccount()
    {
        await using var dbContext = CreateDbContext();
        await SeedRolesAndActorAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var request = new AdminController.AdminCreateAccountRequest
        {
            Username = "AdminOperations",
            FullName = "Operations Lead",
            Email = "ops.admin@example.com",
            Password = "StrongPass9",
            ConfirmPassword = "StrongPass9"
        };

        var result = await controller.CreateAdminUser(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);

        var createdUser = await dbContext.Users
            .Include(x => x.Role)
            .SingleAsync(x => x.Username == "AdminOperations");

        Assert.Equal("Admin", createdUser.Role.RoleName);
        Assert.Equal("ops.admin@example.com", createdUser.Email);
        Assert.True(createdUser.IsEmailVerified);
        Assert.False(createdUser.IsLocked);
        Assert.True(PasswordHashUtility.VerifyPassword("StrongPass9", createdUser.PasswordHash));

        var audit = await dbContext.AdminAuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync();

        Assert.Equal("CreateAdmin", audit.ActionType);
        Assert.Equal(createdUser.UserId.ToString(), audit.TargetId);
    }

    [Fact]
    public async Task CreateAdminUser_ReturnsValidationProblem_WhenPasswordIsWeak()
    {
        await using var dbContext = CreateDbContext();
        await SeedRolesAndActorAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var request = new AdminController.AdminCreateAccountRequest
        {
            Username = "admin2",
            FullName = "Admin Two",
            Email = "admin.two@example.com",
            Password = "weak",
            ConfirmPassword = "weak"
        };

        var result = await controller.CreateAdminUser(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains(nameof(AdminController.AdminCreateAccountRequest.Password), problem.Errors.Keys);
    }

    [Fact]
    public async Task GetAdminUsers_ReturnsOnlyAdminAccounts()
    {
        await using var dbContext = CreateDbContext();
        await SeedRolesAndActorAsync(dbContext);
        await SeedRegularUserAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.GetAdminUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal("Admin", items[0].GetProperty("Username").GetString());
        Assert.Equal(1, root.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task DeleteUser_SoftDeletesRecord_InsteadOfRemovingIt()
    {
        await using var dbContext = CreateDbContext();
        await SeedRolesAndActorAsync(dbContext);
        await SeedRegularUserAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.DeleteUser(
            8,
            new AdminController.AdminDeleteUserRequest { Reason = "Ẩn khỏi hệ thống" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var message = ok.Value?.GetType().GetProperty("message")?.GetValue(ok.Value)?.ToString();
        Assert.Equal("Đã ẩn tài khoản người dùng khỏi hệ thống.", message);

        var hiddenUser = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleAsync(x => x.UserId == 8);

        Assert.False(hiddenUser.Status);
        Assert.True(hiddenUser.IsLocked);
        Assert.False(await dbContext.Users.AnyAsync(x => x.UserId == 8));
    }

    [Fact]
    public async Task UpdateUser_PreservesPremiumState_WhenEditingAccountFields()
    {
        await using var dbContext = CreateDbContext();
        await SeedRolesAndActorAsync(dbContext);
        await SeedPremiumUserAsync(dbContext);
        var controller = CreateController(dbContext, userId: 7, role: "Admin");

        var result = await controller.UpdateUser(
            9,
            new AdminController.AdminUpdateUserRequest
            {
                Username = "premium.user",
                FullName = "Premium User Updated",
                Email = "premium.updated@example.com",
                Role = "User",
                IsLocked = true,
                IsEmailVerified = true
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var user = await dbContext.Users.SingleAsync(x => x.UserId == 9);
        Assert.True(user.IsPremium);
        Assert.Equal("Premium", user.SubscriptionTier);
        Assert.NotNull(user.PremiumStartedAt);
        Assert.NotNull(user.PremiumExpiresAt);
        Assert.True(user.IsLocked);
        Assert.Equal("premium.updated@example.com", user.Email);
    }

    private static async Task SeedRolesAndActorAsync(AppDbContext dbContext)
    {
        var adminRole = new Role
        {
            RoleId = 1,
            RoleName = "Admin"
        };

        var userRole = new Role
        {
            RoleId = 2,
            RoleName = "User"
        };

        var actor = new User
        {
            UserId = 7,
            Username = "Admin",
            FullName = "System Admin",
            Email = "admin@local",
            PasswordHash = PasswordHashUtility.HashPassword("Thanhhiep8125"),
            RoleId = adminRole.RoleId,
            IsEmailVerified = true,
            IsLocked = false,
            CreatedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        dbContext.Roles.AddRange(adminRole, userRole);
        dbContext.Users.Add(actor);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedRegularUserAsync(AppDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            UserId = 8,
            Username = "normal.user",
            FullName = "Normal User",
            Email = "user@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("StrongPass9"),
            RoleId = 2,
            IsEmailVerified = true,
            IsLocked = false,
            CreatedAt = new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPremiumUserAsync(AppDbContext dbContext)
    {
        var startedAt = new DateTime(2030, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var expiresAt = startedAt.AddDays(30);
        dbContext.Users.Add(new User
        {
            UserId = 9,
            Username = "premium.user",
            FullName = "Premium User",
            Email = "premium@example.com",
            PasswordHash = PasswordHashUtility.HashPassword("StrongPass9"),
            RoleId = 2,
            IsEmailVerified = true,
            IsLocked = false,
            IsPremium = true,
            SubscriptionTier = "Premium",
            PremiumStartedAt = startedAt,
            PremiumExpiresAt = expiresAt,
            CreatedAt = startedAt
        });

        await dbContext.SaveChangesAsync();
    }

    private static AdminController CreateController(AppDbContext dbContext, int? userId, string role)
    {
        var controller = new AdminController(
            dbContext,
            new FakeAiRuntimeSettingsService(),
            new NoOpSummaryProcessingService());
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
            .UseInMemoryDatabase($"admin-controller-account-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
