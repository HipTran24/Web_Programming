using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models;
using Web_Project.Services.Premium;
using Web_Project.Services.Quiz;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly IQuizGenerationService _quizGenerationService;

        public QuizController(IQuizGenerationService quizGenerationService)
        {
            _quizGenerationService = quizGenerationService;
        }

        [HttpPost("generate")]
        public async Task<ActionResult<GenerateQuizResponse>> Generate(
            [FromBody] GenerateQuizRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var userId = ResolveCurrentUserId();
                var requestIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var userAgent = Request.Headers.UserAgent.ToString();
                var result = await _quizGenerationService.GenerateQuizAsync(
                    request,
                    userId,
                    requestIp,
                    userAgent,
                    cancellationToken);
                return Ok(result);
            }
            catch (TokenQuotaExceededException ex)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    message = ex.Message,
                    dailyTokenLimit = ex.Limit,
                    tokenUsedToday = ex.UsedToday
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("{quizId:int}")]
        public async Task<ActionResult<GenerateQuizResponse>> GetQuiz(
            int quizId,
            CancellationToken cancellationToken)
        {
            if (quizId <= 0)
            {
                return BadRequest(new { message = "QuizId không hợp lệ." });
            }

            var userId = ResolveCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem quiz." });
            }

            try
            {
                var quiz = await _quizGenerationService.GetQuizAsync(quizId, userId, cancellationToken);
                if (quiz is null)
                {
                    return NotFound(new { message = "Không tìm thấy quiz." });
                }

                return Ok(quiz);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("latest")]
        public async Task<ActionResult<GenerateQuizResponse>> GetLatestQuiz(
            CancellationToken cancellationToken)
        {
            var userId = ResolveCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để xem quiz." });
            }

            try
            {
                var quiz = await _quizGenerationService.GetLatestQuizAsync(userId, cancellationToken);
                if (quiz is null)
                {
                    return NotFound(new { message = "Chưa có quiz nào để hiển thị." });
                }

                return Ok(quiz);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        [HttpPost("submit")]
        public async Task<ActionResult<SubmitQuizResponse>> Submit(
            [FromBody] SubmitQuizRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var userId = ResolveCurrentUserId();
                var result = await _quizGenerationService.SubmitQuizAsync(request, userId, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        private int? ResolveCurrentUserId()
        {
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdRaw, out var userId) ? userId : null;
        }
    }
}
