using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models.Dtos.User;
using Web_Project.Services.Users;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IUserService _userService;

        public ProfileController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<ProfileResponse>> GetProfile(
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _userService.GetProfileAsync(int.Parse(userId), cancellationToken);

            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(result.Response);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _userService.UpdateProfileAsync(
                int.Parse(userId),
                request,
                cancellationToken);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Profile updated successfully" });
        }
    }
}