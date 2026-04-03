using System.Security.Claims;

namespace Web_Project.Middleware
{
    public class PortalRoutingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IWebHostEnvironment _environment;

        public PortalRoutingMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path;

            if (path.Equals("/home/admin.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/admin");
                return;
            }

            if (path.Equals("/home/dashboard.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/dashboard");
                return;
            }

            if (path.Equals("/unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/home/unauthorized.html");
                return;
            }

            if (path.Equals("/admin/login", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/admin/login/", StringComparison.OrdinalIgnoreCase))
            {
                HandleSharedLoginRedirect(context);
                return;
            }

            if (IsAdminPortalPath(path))
            {
                await HandleAdminPortalAsync(context);
                return;
            }

            if (path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/dashboard/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUserDashboardAsync(context);
                return;
            }

            if (IsUserPortalPage(path))
            {
                await HandleUserPortalPageAsync(context);
                return;
            }

            await _next(context);
        }

        private void HandleSharedLoginRedirect(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                context.Response.Redirect(IsAdmin(context.User) ? "/admin" : "/dashboard");
                return;
            }

            var requestedReturnUrl = context.Request.Query["returnUrl"].ToString();
            var target = string.IsNullOrWhiteSpace(requestedReturnUrl) ? "/admin" : requestedReturnUrl;
            var normalizedTarget = Uri.EscapeDataString(target);
            context.Response.Redirect($"/home/login.html?returnUrl={normalizedTarget}");
        }

        private async Task HandleAdminPortalAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
                context.Response.Redirect($"/home/login.html?returnUrl={returnUrl}");
                return;
            }

            if (!IsAdmin(context.User))
            {
                context.Response.Redirect("/unauthorized");
                return;
            }

            var pageFileName = ResolveAdminPageFileName(context.Request.Path);
            await SendHtmlAsync(context, Path.Combine(_environment.ContentRootPath, "PrivatePages", "admin", pageFileName));
        }

        private async Task HandleUserDashboardAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect("/home/login.html?returnUrl=%2Fdashboard");
                return;
            }

            if (IsAdmin(context.User))
            {
                context.Response.Redirect("/admin");
                return;
            }

            await SendHtmlAsync(context, Path.Combine(_environment.ContentRootPath, "PrivatePages", "user", "dashboard.html"));
        }

        private async Task HandleUserPortalPageAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
                context.Response.Redirect($"/home/login.html?returnUrl={returnUrl}");
                return;
            }

            if (IsAdmin(context.User))
            {
                context.Response.Redirect("/admin");
                return;
            }

            await _next(context);
        }

        private static bool IsUserPortalPage(PathString path)
        {
            if (!path.StartsWithSegments("/home", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var value = path.Value ?? string.Empty;
            if (!value.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !value.Equals("/home/index.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/about.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/guide.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/login.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/register.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/otp.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/upload.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/admin.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/dashboard.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/unauthorized.html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdminPortalPath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/admin", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/dashboard", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/dashboard/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/users", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/users/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/content", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/content/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/reports", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/reports/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/ai-system", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/ai-system/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/notifications", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/notifications/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/settings", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/settings/", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/profile", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/admin/profile/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAdminPageFileName(PathString path)
        {
            var value = (path.Value ?? string.Empty).TrimEnd('/');
            return value.ToLowerInvariant() switch
            {
                "/admin/users" => "users.html",
                "/admin/content" => "content.html",
                "/admin/reports" => "reports.html",
                "/admin/ai-system" => "ai-system.html",
                "/admin/notifications" => "notifications.html",
                "/admin/settings" => "settings.html",
                "/admin/profile" => "profile.html",
                "/admin/dashboard" => "index.html",
                "/admin" => "index.html",
                _ => "index.html"
            };
        }

        private static bool IsAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole("Admin") ||
                   string.Equals(user.FindFirstValue("isAdmin"), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task SendHtmlAsync(HttpContext context, string filePath)
        {
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Page not found.");
                return;
            }

            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(filePath);
        }
    }
}
