using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Web_Project.Middleware;

namespace Web_Project.Tests.Admin;

public sealed class PortalRoutingMiddlewareTests
{
    [Theory]
    [InlineData("/admin", "admin-dashboard")]
    [InlineData("/admin/dashboard", "admin-dashboard")]
    [InlineData("/admin/users", "admin-users")]
    [InlineData("/admin/content", "admin-content")]
    [InlineData("/admin/reports", "admin-reports")]
    [InlineData("/admin/settings", "admin-settings")]
    [InlineData("/admin/profile", "admin-profile")]
    public async Task InvokeAsync_ServesExpectedAdminPage_ForAdminRole(string path, string expectedMarker)
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext(path, userId: 1, role: "Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var html = await reader.ReadToEndAsync();
        Assert.Contains(expectedMarker, html);
    }

    [Fact]
    public async Task InvokeAsync_RedirectsAnonymousUser_ToSharedLogin()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/home/login.html?returnUrl=%2Fadmin", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsLegacyAdminLoginRoute_ToSharedLogin()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/admin/login");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/home/login.html?returnUrl=%2Fadmin", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsRegularUser_ToUnauthorized()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/admin", userId: 12, role: "User");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/unauthorized", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ServesAdminPortal_ForAdminRole()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/admin", userId: 1, role: "Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_RedirectsAnonymousUser_FromUserPortalPage_ToLogin()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/home/profile.html", queryString: "?tab=security");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/home/login.html?returnUrl=%2Fhome%2Fprofile.html%3Ftab%3Dsecurity", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RedirectsAdmin_FromUserPortalPage_ToAdminPortal()
    {
        using var fixture = new PortalFixture();
        var middleware = new PortalRoutingMiddleware(_ => Task.CompletedTask, fixture.Environment);
        var context = fixture.CreateContext("/home/profile.html", userId: 1, role: "Admin");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/admin", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_AllowsRegularUser_ToReachUserPortalPage()
    {
        using var fixture = new PortalFixture();
        var nextWasCalled = false;
        var middleware = new PortalRoutingMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        }, fixture.Environment);
        var context = fixture.CreateContext("/home/profile.html", userId: 12, role: "User");

        await middleware.InvokeAsync(context);

        Assert.True(nextWasCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private sealed class PortalFixture : IDisposable
    {
        private readonly string _rootPath;

        public PortalFixture()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), $"portal-routing-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(_rootPath, "PrivatePages", "admin"));
            Directory.CreateDirectory(Path.Combine(_rootPath, "PrivatePages", "user"));
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "index.html"), "<html>admin-dashboard</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "users.html"), "<html>admin-users</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "content.html"), "<html>admin-content</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "reports.html"), "<html>admin-reports</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "settings.html"), "<html>admin-settings</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "admin", "profile.html"), "<html>admin-profile</html>");
            File.WriteAllText(Path.Combine(_rootPath, "PrivatePages", "user", "dashboard.html"), "<html>dashboard</html>");

            Environment = new TestWebHostEnvironment
            {
                ContentRootPath = _rootPath,
                WebRootPath = _rootPath,
                ApplicationName = "PortalTests",
                EnvironmentName = "Development",
                ContentRootFileProvider = new PhysicalFileProvider(_rootPath),
                WebRootFileProvider = new PhysicalFileProvider(_rootPath)
            };
        }

        public IWebHostEnvironment Environment { get; }

        public DefaultHttpContext CreateContext(string path, int? userId = null, string? role = null, string? queryString = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            if (!string.IsNullOrWhiteSpace(queryString))
            {
                context.Request.QueryString = new QueryString(queryString);
            }
            context.Response.Body = new MemoryStream();

            if (userId.HasValue && !string.IsNullOrWhiteSpace(role))
            {
                var identity = new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                        new Claim(ClaimTypes.Role, role)
                    ],
                    authenticationType: "TestAuth");
                context.User = new ClaimsPrincipal(identity);
            }

            return context;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
