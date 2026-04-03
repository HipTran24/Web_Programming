using Microsoft.AspNetCore.Http;

namespace Web_Project.Security
{
    public static class AuthCookieHelper
    {
        public const string CookieName = "wedproject.auth";

        public static void AppendAuthCookie(
            HttpContext httpContext,
            string accessToken,
            DateTime expiresAt,
            bool rememberMe)
        {
            httpContext.Response.Cookies.Append(
                CookieName,
                accessToken,
                BuildCookieOptions(httpContext, rememberMe ? expiresAt : null));
        }

        public static void DeleteAuthCookie(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(
                CookieName,
                BuildCookieOptions(httpContext, DateTime.UnixEpoch));
        }

        private static CookieOptions BuildCookieOptions(HttpContext httpContext, DateTime? expiresAt)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                Expires = expiresAt
            };
        }
    }
}
