using Microsoft.AspNetCore.Mvc;
using Web_Project.Models;
using Web_Project.Services.Content;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : ControllerBase
    {
        private readonly ISummaryProcessingService _summaryProcessingService;
        private readonly ILogger<SummaryController> _logger;

        public SummaryController(
            ISummaryProcessingService summaryProcessingService,
            ILogger<SummaryController> logger)
        {
            _summaryProcessingService = summaryProcessingService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(104_857_600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
        public async Task<ActionResult<SummarizeUploadResponse>> SummarizeUpload(
            [FromForm] IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null && Request.HasFormContentType)
            {
                file = Request.Form.Files.FirstOrDefault();
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Bạn chưa gửi file hợp lệ. Dùng form-data, key nên là 'file'."
                });
            }

            try
            {
                var result = await _summaryProcessingService.SummarizeUploadAsync(file, cancellationToken);
                return Ok(result);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to summarize uploaded file: {FileName}", file.FileName);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Lỗi nội bộ khi xử lý tóm tắt nội dung."
                });
            }
        }

        [HttpPost("text")]
        public async Task<ActionResult<SummarizeUploadResponse>> SummarizeText(
            [FromBody] SummarizeTextRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var result = await _summaryProcessingService.SummarizeTextAsync(
                    request.Text,
                    request.SourceHint,
                    cancellationToken);

                return Ok(result);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to summarize text input.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Lỗi nội bộ khi xử lý tóm tắt văn bản."
                });
            }
        }

        [HttpPost("from-url")]
        public async Task<ActionResult<SummarizeUrlResponse>> SummarizeFromUrl(
            [FromBody] SummarizeUrlRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var result = await _summaryProcessingService.SummarizeFromUrlAsync(request.Url, cancellationToken);
                return Ok(result);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to summarize URL: {Url}", request.Url);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Lỗi nội bộ khi xử lý tóm tắt URL."
                });
            }
        }
    }
}
