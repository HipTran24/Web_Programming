using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Services.Auth;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(
            IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var result = await _authService.LoginAsync(request, cancellationToken);
            if (!result.Success)
            {
                if (result.ValidationErrors.Count > 0)
                {
                    AddValidationErrors(result.ValidationErrors);
                    return ValidationProblem(ModelState);
                }

                return StatusCode(result.StatusCode, new { message = result.Message });
            }

            return Ok(result.Response);
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<LoginResponse>> GoogleLogin(
            [FromBody] GoogleLoginRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var result = await _authService.GoogleLoginAsync(request, cancellationToken);
            if (!result.Success)
            {
                if (result.ValidationErrors.Count > 0)
                {
                    AddValidationErrors(result.ValidationErrors);
                    return ValidationProblem(ModelState);
                }

                return StatusCode(result.StatusCode, new { message = result.Message });
            }

            return Ok(result.Response);
        }

        [HttpPost("google-login-redirect")]
        public async Task<IActionResult> GoogleLoginRedirect(
            [FromForm(Name = "credential")] string credential,
            [FromQuery] string? returnUrl,
            CancellationToken cancellationToken)
        {
            var request = new GoogleLoginRequest
            {
                IdToken = credential,
                RememberMe = true
            };

            var result = await _authService.GoogleLoginAsync(request, cancellationToken);
            if (!result.Success || result.Response is null)
            {
                var failureMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "Đăng nhập Google thất bại."
                    : result.Message;
                var fallbackUrl = $"/home/login.html?message={Uri.EscapeDataString(failureMessage)}";
                return BuildGoogleRedirectPage(false, failureMessage, null, fallbackUrl);
            }

            var safeReturnUrl = NormalizeReturnUrl(returnUrl);
            return BuildGoogleRedirectPage(true, "Đăng nhập Google thành công.", result.Response, safeReturnUrl);
        }

        [HttpGet("google-config")]
        public IActionResult GoogleConfig([FromServices] IOptions<GoogleAuthSettings> googleAuthSettings)
        {
            var clientId = googleAuthSettings.Value.ClientId?.Trim() ?? string.Empty;

            return Ok(new
            {
                enabled = !string.IsNullOrWhiteSpace(clientId),
                clientId
            });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var role = User.FindFirstValue(ClaimTypes.Role);

            return Ok(new
            {
                userId,
                username,
                email,
                role
            });
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var requestIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var result = await _authService.RegisterAsync(request, requestIp, cancellationToken);

            if (!result.Success)
            {
                if (result.IsConflict)
                {
                    return Conflict(new { message = result.Message });
                }

                if (result.ValidationErrors.Count > 0)
                {
                    AddValidationErrors(result.ValidationErrors);
                    return ValidationProblem(ModelState);
                }

                return BadRequest(new { message = result.Message });
            }

            var response = result.Response!;
            return Created($"/api/auth/users/{response.UserId}", response);
        }

        [HttpPost("verify-email-otp")]
        public async Task<IActionResult> VerifyEmailOtp(
            [FromBody] VerifyEmailOtpRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var result = await _authService.VerifyEmailOtpAsync(request, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = result.Message });
        }

        [HttpPost("resend-email-otp")]
        public async Task<IActionResult> ResendEmailOtp(
            [FromBody] ResendEmailOtpRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var requestIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var result = await _authService.ResendEmailOtpAsync(request, requestIp, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                message = result.Message,
                expiresAt = result.ExpiresAt
            });
        }

        private void AddValidationErrors(Dictionary<string, string[]> errors)
        {
            foreach (var pair in errors)
            {
                foreach (var message in pair.Value)
                {
                    ModelState.AddModelError(pair.Key, message);
                }
            }
        }

                private static string NormalizeReturnUrl(string? value)
                {
                        var raw = (value ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                                return "/home/index.html";
                        }

                        if (!raw.StartsWith('/') || raw.StartsWith("//", StringComparison.Ordinal))
                        {
                                return "/home/index.html";
                        }

                        return raw;
                }

                private static ContentResult BuildGoogleRedirectPage(
                        bool success,
                        string message,
                        LoginResponse? response,
                        string redirectUrl)
                {
                        var safeMessage = JsonSerializer.Serialize(message ?? string.Empty);
                        var safeRedirectUrl = JsonSerializer.Serialize(redirectUrl ?? "/home/index.html");
                        var payload = JsonSerializer.Serialize(response);

                        var html = """
<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Google Sign-In</title>
</head>
<body>
    <script>
        (function () {
            var tokenStorageKey = "auth.accessToken";
            var userStorageKey = "auth.currentUser";
            var success = __SUCCESS__;
            var message = __MESSAGE__;
            var redirectUrl = __REDIRECT_URL__;
            var data = __PAYLOAD__;

            if (success && data && data.accessToken) {
                try {
                    var storedReturnUrl = window.sessionStorage.getItem("auth.google.returnUrl") || "";
                    window.sessionStorage.removeItem("auth.google.returnUrl");

                    window.localStorage.setItem(tokenStorageKey, data.accessToken);
                    window.localStorage.setItem(userStorageKey, JSON.stringify({
                        userId: data.userId ?? null,
                        username: data.username ?? "",
                        fullName: data.fullName ?? "",
                        email: data.email ?? "",
                        role: data.role ?? "",
                        expiresAt: data.expiresAt ?? null
                    }));
                    window.sessionStorage.removeItem(tokenStorageKey);
                    window.sessionStorage.removeItem(userStorageKey);
                    window.sessionStorage.removeItem("pendingEmailVerification");

                    if (storedReturnUrl && storedReturnUrl.startsWith("/") && !storedReturnUrl.startsWith("//")) {
                        redirectUrl = storedReturnUrl;
                    }
                } catch (_) {}

                window.location.replace(redirectUrl || "/home/index.html");
                return;
            }

            var target = "/home/login.html";
            if (message) {
                target += "?message=" + encodeURIComponent(message);
            }
            window.location.replace(target);
        })();
    </script>
</body>
</html>
""";

                        html = html
                                .Replace("__SUCCESS__", success ? "true" : "false", StringComparison.Ordinal)
                                .Replace("__MESSAGE__", safeMessage, StringComparison.Ordinal)
                                .Replace("__REDIRECT_URL__", safeRedirectUrl, StringComparison.Ordinal)
                                .Replace("__PAYLOAD__", payload, StringComparison.Ordinal);

                        return new ContentResult
                        {
                                ContentType = "text/html; charset=utf-8",
                                Content = html,
                                StatusCode = 200
                        };
                }
    }
}
