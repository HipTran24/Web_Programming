using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Web_Project.Models;
using Web_Project.Services.Content;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SummaryController : ControllerBase
    {
        private const string GuestTokenHeaderName = "X-Guest-Token";
        private const string GuestTrialLimitMessage = "Tài khoản guest chỉ được dùng 1 lần cho tóm tắt/sinh quiz/chấm điểm. Vui lòng đăng nhập để tiếp tục.";

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
                GuestSession? guestSession = null;
                if (!userId.HasValue)
                {
                    guestSession = await GetOrCreateGuestSessionAsync(cancellationToken);
                    await EnsureGuestCanSummarizeAsync(guestSession, cancellationToken);
                }

                var result = await _summaryProcessingService.SummarizeUploadAsync(
                    file,
                    userId,
                    isGuest: !userId.HasValue,
                    cancellationToken);

                if (guestSession is not null)
                {
                    await RegisterGuestSummaryUsageAsync(guestSession, cancellationToken);
                    Response.Headers[GuestTokenHeaderName] = guestSession.GuestToken;
                }

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
                GuestSession? guestSession = null;
                if (!userId.HasValue)
                {
                    guestSession = await GetOrCreateGuestSessionAsync(cancellationToken);
                    await EnsureGuestCanSummarizeAsync(guestSession, cancellationToken);
                }

                var result = await _summaryProcessingService.SummarizeTextAsync(
                    request.Text,
                    request.SourceHint,
                    userId,
                    isGuest: !userId.HasValue,
                    cancellationToken);

                if (guestSession is not null)
                {
                    await RegisterGuestSummaryUsageAsync(guestSession, cancellationToken);
                    Response.Headers[GuestTokenHeaderName] = guestSession.GuestToken;
                }

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
                GuestSession? guestSession = null;
                if (!userId.HasValue)
                {
                    guestSession = await GetOrCreateGuestSessionAsync(cancellationToken);
                    await EnsureGuestCanSummarizeAsync(guestSession, cancellationToken);
                }

                var result = await _summaryProcessingService.SummarizeFromUrlAsync(
                    request.Url,
                    userId,
                    isGuest: !userId.HasValue,
                    cancellationToken);

                if (guestSession is not null)
                {
                    await RegisterGuestSummaryUsageAsync(guestSession, cancellationToken);
                    Response.Headers[GuestTokenHeaderName] = guestSession.GuestToken;
                }

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
        [Authorize(Policy = "UserOnly")]
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
            var isSqlServer = (_dbContext.Database.ProviderName ?? string.Empty)
                .Contains("SqlServer", StringComparison.OrdinalIgnoreCase);

            var baseQuery = _dbContext
                .Contents
                .Where(x => x.UserId == userId.Value && !x.IsGuest);

            if (!string.IsNullOrWhiteSpace(trimmedQuery))
            {
                if (isSqlServer)
                {
                    const string accentInsensitiveCollation = "Latin1_General_100_CI_AI";
                    baseQuery = baseQuery.Where(x =>
                        EF.Functions.Collate(x.FileName ?? string.Empty, accentInsensitiveCollation).Contains(trimmedQuery) ||
                        EF.Functions.Collate(x.FilePath ?? string.Empty, accentInsensitiveCollation).Contains(trimmedQuery) ||
                        EF.Functions.Collate(x.SourceUrl ?? string.Empty, accentInsensitiveCollation).Contains(trimmedQuery) ||
                        (x.AIProcess != null &&
                         EF.Functions.Collate(x.AIProcess.Summary ?? string.Empty, accentInsensitiveCollation).Contains(trimmedQuery)));
                }
                else
                {
                    var loweredQuery = trimmedQuery.ToLower();
                    baseQuery = baseQuery.Where(x =>
                        (x.FileName ?? string.Empty).ToLower().Contains(loweredQuery) ||
                        (x.FilePath ?? string.Empty).ToLower().Contains(loweredQuery) ||
                        (x.SourceUrl ?? string.Empty).ToLower().Contains(loweredQuery) ||
                        (x.AIProcess != null && (x.AIProcess.Summary ?? string.Empty).ToLower().Contains(loweredQuery)));
                }
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

        private async Task<GuestSession> GetOrCreateGuestSessionAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var requestIp = TrimTo(HttpContext.Connection.RemoteIpAddress?.ToString(), 64);
            var userAgent = TrimTo(Request.Headers.UserAgent.ToString(), 512);
            var requestToken = TrimTo(Request.Headers[GuestTokenHeaderName].ToString(), 128);
            var fingerprintHash = BuildFingerprintHash(requestIp, userAgent);

            GuestSession? session = null;
            if (!string.IsNullOrWhiteSpace(requestToken))
            {
                session = await _dbContext.GuestSessions
                    .FirstOrDefaultAsync(x => x.GuestToken == requestToken, cancellationToken);
            }

            if (session is null && !string.IsNullOrWhiteSpace(fingerprintHash))
            {
                session = await _dbContext.GuestSessions
                    .Where(x => x.FingerprintHash == fingerprintHash)
                    .OrderByDescending(x => x.LastSeenAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (session is not null)
            {
                if (session.IsBlocked)
                {
                    throw new InvalidOperationException("Guest session đã bị chặn. Vui lòng đăng nhập để tiếp tục.");
                }

                if (!string.IsNullOrWhiteSpace(requestToken))
                {
                    session.GuestToken = requestToken;
                }

                if (!string.IsNullOrWhiteSpace(fingerprintHash))
                {
                    session.FingerprintHash = fingerprintHash;
                }

                session.IpAddress = requestIp;
                session.UserAgent = userAgent;
                session.LastSeenAt = now;
                return session;
            }

            session = new GuestSession
            {
                GuestToken = !string.IsNullOrWhiteSpace(requestToken) ? requestToken : Guid.NewGuid().ToString("N"),
                FingerprintHash = fingerprintHash,
                IpAddress = requestIp,
                UserAgent = userAgent,
                FirstSeenAt = now,
                LastSeenAt = now,
                IsBlocked = false,
                TrialUsedAt = null
            };

            _dbContext.GuestSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return session;
        }

        private async Task EnsureGuestCanSummarizeAsync(GuestSession session, CancellationToken cancellationToken)
        {
            if (session.TrialUsedAt.HasValue)
            {
                throw new InvalidOperationException(GuestTrialLimitMessage);
            }

            var usedSummary = await _dbContext.DailyUsageCounters
                .AnyAsync(x => x.GuestSessionId == session.GuestSessionId && x.AIProcessCount > 0, cancellationToken);
            if (usedSummary)
            {
                throw new InvalidOperationException(GuestTrialLimitMessage);
            }
        }

        private async Task RegisterGuestSummaryUsageAsync(GuestSession session, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var usageDate = now.Date;

            var counter = await _dbContext.DailyUsageCounters
                .FirstOrDefaultAsync(
                    x => x.GuestSessionId == session.GuestSessionId && x.UsageDate == usageDate,
                    cancellationToken);

            if (counter is null)
            {
                counter = new DailyUsageCounter
                {
                    UsageDate = usageDate,
                    UserId = null,
                    GuestSessionId = session.GuestSessionId,
                    UploadCount = 0,
                    AIProcessCount = 0,
                    QuizGenerationCount = 0,
                    TotalProcessingTime = 0,
                    UpdatedAt = now
                };

                _dbContext.DailyUsageCounters.Add(counter);
            }

            counter.AIProcessCount += 1;
            counter.UpdatedAt = now;
            session.LastSeenAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string BuildFingerprintHash(string ip, string userAgent)
        {
            var raw = $"{ip}|{userAgent}".Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw == "|")
            {
                return string.Empty;
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string TrimTo(string? value, int maxLength)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
