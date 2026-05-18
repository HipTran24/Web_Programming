using System.Security.Claims;
using Web_Project.Services.Payments;

namespace Web_Project.Middleware
{
    public class PremiumAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public PremiumAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IPremiumSubscriptionService premiumSubscriptionService,
            IPayOSPaymentService payOSPaymentService)
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/premium", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (IsPremiumOpenPage(path))
            {
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
                await _next(context);
                return;
            }

            var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdRaw, out var userId))
            {
                context.Response.Redirect("/home/login.html");
                return;
            }

            var status = await premiumSubscriptionService.GetStatusAsync(userId, context.RequestAborted);
            if (!status.IsPremium)
            {
                var syncResult = await payOSPaymentService.SyncLatestPendingPaymentAsync(userId, context.RequestAborted);
                if (syncResult.Paid)
                {
                    await _next(context);
                    return;
                }

                var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
                context.Response.Redirect($"/premium/checkout.html?returnUrl={returnUrl}");
                return;
            }

            await _next(context);
        }

        private static bool IsAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole("Admin") ||
                   string.Equals(user.FindFirstValue("isAdmin"), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPremiumOpenPage(PathString path)
        {
            var value = path.Value ?? string.Empty;
            return value.Equals("/premium/upgrade.html", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/premium/checkout.html", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/premium/payment-success.html", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("/premium/payment-failed.html", StringComparison.OrdinalIgnoreCase)
                   || value.StartsWith("/premium/assets", StringComparison.OrdinalIgnoreCase);
        }
    }
}
