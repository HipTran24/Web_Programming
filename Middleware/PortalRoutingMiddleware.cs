using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;

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

            if (IsPremiumAssetPath(path))
            {
                await _next(context);
                return;
            }

            if (IsPremiumPortalPath(path))
            {
                await HandlePremiumPortalAsync(context);
                return;
            }

            if (path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/dashboard/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUserDashboardAsync(context);
                return;
            }

            if (path.Equals("/home/premium-upgrade.html", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePremiumUpgradePageAsync(context);
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

            if (await IsPremiumActiveAsync(context))
            {
                context.Response.Redirect("/premium/dashboard.html");
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

        private async Task HandlePremiumUpgradePageAsync(HttpContext context)
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

            if (await IsPremiumActiveAsync(context))
            {
                context.Response.Redirect("/premium/account.html");
                return;
            }

            await _next(context);
        }

        private async Task HandlePremiumPortalAsync(HttpContext context)
        {
            var path = context.Request.Path;

            if (IsPremiumPublicPage(path))
            {
                if (context.User.Identity?.IsAuthenticated == true && !IsAdmin(context.User))
                {
                    if (await IsPremiumActiveAsync(context))
                    {
                        context.Response.Redirect("/premium/account.html");
                        return;
                    }
                }

                await _next(context);
                return;
            }

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

            if (IsPremiumAccountOrPaymentPage(path))
            {
                if (await IsPremiumActiveAsync(context))
                {
                    context.Response.Redirect("/premium/account.html");
                    return;
                }

                await _next(context);
                return;
            }

            var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdRaw, out var userId))
            {
                context.Response.Redirect("/home/login.html?returnUrl=%2Fpremium%2Fupgrade.html");
                return;
            }

            if (!await IsPremiumActiveAsync(context, userId))
            {
                var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
                context.Response.Redirect($"/premium/upgrade.html?returnUrl={returnUrl}");
                return;
            }

            await _next(context);
        }

        private static async Task<bool> IsPremiumActiveAsync(HttpContext context, int? userId = null)
        {
            var resolvedUserId = userId;
            if (!resolvedUserId.HasValue)
            {
                var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdRaw, out var parsedUserId))
                {
                    return false;
                }

                resolvedUserId = parsedUserId;
            }

            var dbContext = context.RequestServices.GetService<AppDbContext>();
            if (dbContext is null)
            {
                return false;
            }

            var user = await dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == resolvedUserId.Value)
                .Select(x => new { x.IsPremium, x.SubscriptionTier, x.PremiumExpiresAt })
                .FirstOrDefaultAsync(context.RequestAborted);

            return user is not null &&
                (user.IsPremium || string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase)) &&
                (!user.PremiumExpiresAt.HasValue || user.PremiumExpiresAt.Value > DateTime.UtcNow);
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
                   && !value.Equals("/home/admin.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/dashboard.html", StringComparison.OrdinalIgnoreCase)
                   && !value.Equals("/home/unauthorized.html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPremiumAssetPath(PathString path)
        {
            return path.StartsWithSegments("/premium/assets", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPremiumPortalPath(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.StartsWith("/premium/", StringComparison.OrdinalIgnoreCase) &&
                   value.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPremiumPublicPage(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/premium/upgrade.html", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPremiumAccountOrPaymentPage(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/premium/checkout.html", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("/premium/payment-success.html", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("/premium/payment-failed.html", StringComparison.OrdinalIgnoreCase);
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
