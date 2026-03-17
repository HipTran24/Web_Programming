using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Web_Project.Models;
using Web_Project.Services.Content;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : ControllerBase
    {
        private readonly ISummaryProcessingService _summaryProcessingService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<SummaryController> _logger;

        public SummaryController(
            ISummaryProcessingService summaryProcessingService,
            AppDbContext dbContext,
            ILogger<SummaryController> logger)
        {
            _summaryProcessingService = summaryProcessingService;
            _dbContext = dbContext;
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
                var userId = TryGetCurrentUserId();
                var result = await _summaryProcessingService.SummarizeUploadAsync(
                    file,
                    userId,
                    isGuest: !userId.HasValue,
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
                var userId = TryGetCurrentUserId();
                var result = await _summaryProcessingService.SummarizeTextAsync(
                    request.Text,
                    request.SourceHint,
                    userId,
                    isGuest: !userId.HasValue,
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
                var userId = TryGetCurrentUserId();
                var result = await _summaryProcessingService.SummarizeFromUrlAsync(
                    request.Url,
                    userId,
                    isGuest: !userId.HasValue,
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
                _logger.LogError(ex, "Failed to summarize URL: {Url}", request.Url);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "Lỗi nội bộ khi xử lý tóm tắt URL."
                });
            }
        }

        [HttpGet("upload-history")]
        [Authorize]
        public async Task<ActionResult<object>> GetUploadHistory(
            [FromQuery] string? query,
            [FromQuery] string? sourceType,
            [FromQuery] string? sort,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            CancellationToken cancellationToken = default)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var safePage = page < 1 ? 1 : page;
            var safePageSize = Math.Clamp(pageSize, 1, 48);
            var trimmedQuery = query?.Trim();
            var trimmedSourceType = sourceType?.Trim();
            var normalizedSort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)
                ? "oldest"
                : "latest";

            var baseQuery = _dbContext
                .Contents
                .Where(x => x.UserId == userId.Value && !x.IsGuest);

            if (!string.IsNullOrWhiteSpace(trimmedQuery))
            {
                baseQuery = baseQuery.Where(x => x.FileName.Contains(trimmedQuery));
            }

            if (!string.IsNullOrWhiteSpace(trimmedSourceType) &&
                !string.Equals(trimmedSourceType, "all", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(x => x.SourceType == trimmedSourceType);
            }

            var statsProjection = baseQuery.Select(x => new
            {
                x.SourceType,
                x.CreatedAt,
                HasAiProcess = x.AIProcess != null
            });

            var totalItems = await baseQuery.CountAsync(cancellationToken);
            var totalSourceCount = await statsProjection.CountAsync(cancellationToken);
            var fileUploadCount = await statsProjection.CountAsync(x => x.SourceType == "FileUpload", cancellationToken);
            var urlCount = await statsProjection.CountAsync(x => x.SourceType != "FileUpload", cancellationToken);
            var aiCount = await statsProjection.CountAsync(x => x.HasAiProcess, cancellationToken);
            var latestUploadAt = await statsProjection
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var orderedQuery = normalizedSort == "oldest"
                ? baseQuery.OrderBy(x => x.CreatedAt)
                : baseQuery.OrderByDescending(x => x.CreatedAt);

            var rawItems = await orderedQuery
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.ContentId,
                    x.FileName,
                    x.FilePath,
                    x.FileType,
                    x.SourceType,
                    x.SourceUrl,
                    x.FetchStatus,
                    x.FetchError,
                    x.CreatedAt,
                    HasAiProcess = x.AIProcess != null,
                    Summary = x.AIProcess != null ? x.AIProcess.Summary : string.Empty,
                    ProcessingTimeSeconds = x.AIProcess != null
                        ? x.AIProcess.ProcessingTime
                        : 0
                })
                .ToListAsync(cancellationToken);

            var items = rawItems.Select(x => new
            {
                x.ContentId,
                x.FileName,
                x.FilePath,
                x.FileType,
                x.SourceType,
                x.SourceUrl,
                x.FetchStatus,
                x.FetchError,
                x.CreatedAt,
                x.HasAiProcess,
                SummaryPreview = string.IsNullOrWhiteSpace(x.Summary)
                    ? string.Empty
                    : x.Summary[..Math.Min(x.Summary.Length, 180)],
                x.ProcessingTimeSeconds
            });

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = (int)Math.Ceiling(totalItems / (double)safePageSize),
                stats = new
                {
                    totalUploads = totalSourceCount,
                    fileUploads = fileUploadCount,
                    urlUploads = urlCount,
                    aiCompleted = aiCount,
                    latestUploadAt
                },
                items
            });
        }

        private int? TryGetCurrentUserId()
        {
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdRaw, out var userId) ? userId : null;
        }
    }
}
