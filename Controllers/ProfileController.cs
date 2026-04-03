using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models.Dtos.User;
using Web_Project.Services.Notifications;
using Web_Project.Services.Users;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize(Policy = "UserOnly")]
    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ISystemNotificationService? _systemNotificationService;

        public ProfileController(
            IUserService userService,
            ISystemNotificationService? systemNotificationService = null)
        {
            _userService = userService;
            _systemNotificationService = systemNotificationService;
        }

        [HttpGet]
        public async Task<ActionResult<ProfileResponse>> GetProfile(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.GetProfileAsync(parsedUserId, cancellationToken);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(result.Response);
        }

        [HttpPut]
        public async Task<ActionResult<ProfileResponse>> UpdateProfile(
            [FromBody] UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.UpdateProfileAsync(
                parsedUserId,
                request,
                cancellationToken);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new
            {
                message = result.Message,
                profile = result.Response
            });
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.ChangePasswordAsync(parsedUserId, request, cancellationToken);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        [HttpGet("notifications")]
        public async Task<ActionResult<NotificationSettingsResponse>> GetNotificationSettings(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.GetNotificationSettingsAsync(parsedUserId, cancellationToken);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result.Response);
        }

        [HttpPut("notifications")]
        public async Task<ActionResult<NotificationSettingsResponse>> UpdateNotificationSettings(
            [FromBody] NotificationSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.UpdateNotificationSettingsAsync(parsedUserId, request, cancellationToken);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new
            {
                message = result.Message,
                settings = result.Response
            });
        }

        [HttpGet("system-notifications")]
        public async Task<ActionResult<UserSystemNotificationInboxResponse>> GetSystemNotifications(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            if (_systemNotificationService is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ thông báo hệ thống chưa sẵn sàng." });

            var result = await _systemNotificationService.GetUserInboxAsync(parsedUserId, cancellationToken);
            return Ok(result);
        }

        [HttpGet("system-notifications/unread")]
        public async Task<ActionResult<UserSystemNotificationUnreadResponse>> GetSystemNotificationUnreadSummary(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            if (_systemNotificationService is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ thông báo hệ thống chưa sẵn sàng." });

            var result = await _systemNotificationService.GetUnreadSummaryAsync(parsedUserId, cancellationToken);
            return Ok(result);
        }

        [HttpPut("system-notifications/read")]
        public async Task<IActionResult> MarkSystemNotificationsAsRead(
            [FromBody] MarkSystemNotificationReadRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            if (_systemNotificationService is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ thông báo hệ thống chưa sẵn sàng." });

            var result = await _systemNotificationService.MarkAsReadAsync(
                parsedUserId,
                request ?? new MarkSystemNotificationReadRequest { MarkAll = true },
                cancellationToken);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new
            {
                message = result.Message,
                unreadCount = result.UnreadCount
            });
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userId, out var parsedUserId))
                return Unauthorized();

            var result = await _userService.DeleteAccountAsync(parsedUserId, cancellationToken);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }
    }
}
