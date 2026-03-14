using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wed_Project.Models;
using Wed_Project.Services.Auth;

namespace Wed_Project.Controllers
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
    }
}
