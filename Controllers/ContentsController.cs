using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContentsController : ControllerBase
    {
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"
        };

        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ContentsController> _logger;

        public ContentsController(
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<ContentsController> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpPost("from-url")]
        public async Task<ActionResult<ContentFromUrlResponse>> CreateFromUrl(
            [FromBody] CreateContentFromUrlRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("URL không hợp lệ. Chỉ chấp nhận http/https.");
            }

            if (await IsBlockedHostAsync(uri, cancellationToken))
            {
                return BadRequest("URL không được phép truy cập vì lý do bảo mật.");
            }

            var now = DateTime.UtcNow;
            var initialSourceType = InferSourceType(uri, string.Empty);
            var content = new Content
            {
                UserId = request.UserId,
                IsGuest = request.IsGuest,
                FileName = GetFileNameFromUrl(uri),
                FileType = initialSourceType,
                FilePath = request.Url,
                SourceType = initialSourceType,
                SourceUrl = request.Url,
                FetchStatus = "Pending",
                FetchError = null,
                ExtractedText = string.Empty,
                AI_DetectedSubject = string.Empty,
                AI_DetectedGrade = string.Empty,
                CreatedAt = now
            };

            _dbContext.Contents.Add(content);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var fetchResult = await FetchAndExtractAsync(uri, cancellationToken);

                content.SourceType = fetchResult.SourceType;
                content.FileType = fetchResult.FileType;
                content.FileName = fetchResult.FileName;
                content.FetchStatus = fetchResult.FetchStatus;
                content.FetchError = fetchResult.FetchError;
                content.ExtractedText = fetchResult.ExtractedText;

                await _dbContext.SaveChangesAsync(cancellationToken);

                return Ok(new ContentFromUrlResponse
                {
                    ContentId = content.ContentId,
                    SourceType = content.SourceType,
                    FetchStatus = content.FetchStatus,
                    Message = fetchResult.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch URL for ContentId={ContentId}", content.ContentId);

                content.FetchStatus = "Failed";
                content.FetchError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return StatusCode(StatusCodes.Status502BadGateway, new ContentFromUrlResponse
                {
                    ContentId = content.ContentId,
                    SourceType = content.SourceType,
                    FetchStatus = content.FetchStatus,
                    Message = "Không thể tải nội dung từ URL."
                });
            }
        }

        private async Task<FetchResult> FetchAndExtractAsync(Uri uri, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("WedProject-UrlFetcher/1.0");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new FetchResult(
                    SourceType: InferSourceType(uri, string.Empty),
                    FileType: "url",
                    FileName: GetFileNameFromUrl(uri),
                    FetchStatus: "Failed",
                    FetchError: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    ExtractedText: string.Empty,
                    Message: "URL không truy cập được.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            var sourceType = InferSourceType(uri, mediaType);
            var fileType = GetFileType(uri, mediaType);
            var fileName = GetFileNameFromUrl(uri);

            if (IsLikelyTextContent(mediaType, uri.AbsolutePath))
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                var extractedText = NormalizeText(payload, mediaType);
                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    return new FetchResult(
                        SourceType: sourceType,
                        FileType: fileType,
                        FileName: fileName,
                        FetchStatus: "Failed",
                        FetchError: "Không trích xuất được văn bản từ URL.",
                        ExtractedText: string.Empty,
                        Message: "Tải URL thành công nhưng không lấy được text.");
                }

                return new FetchResult(
                    SourceType: sourceType,
                    FileType: fileType,
                    FileName: fileName,
                    FetchStatus: "Completed",
                    FetchError: null,
                    ExtractedText: TrimToMax(extractedText, 500000),
                    Message: "Đã trích xuất text từ URL và lưu vào database.");
            }

            return new FetchResult(
                SourceType: sourceType,
                FileType: fileType,
                FileName: fileName,
                FetchStatus: "PendingProcessing",
                FetchError: "Loại URL này cần pipeline xử lý nâng cao (OCR/STT/parser) và chưa bật trong endpoint hiện tại.",
                ExtractedText: string.Empty,
                Message: "Đã lưu URL, đang chờ pipeline xử lý nâng cao.");
        }

        private static string InferSourceType(Uri uri, string mediaType)
        {
            if (mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return "VideoUrl";
            }

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (VideoExtensions.Contains(extension))
            {
                return "VideoUrl";
            }

            if (IsLikelyTextContent(mediaType, uri.AbsolutePath))
            {
                return "TextUrl";
            }

            return "DocumentUrl";
        }

        private static bool IsLikelyTextContent(string mediaType, string absolutePath)
        {
            if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extension = Path.GetExtension(absolutePath);
            return TextExtensions.Contains(extension);
        }

        private static string GetFileType(Uri uri, string mediaType)
        {
            if (!string.IsNullOrWhiteSpace(mediaType))
            {
                return mediaType;
            }

            var extension = Path.GetExtension(uri.AbsolutePath);
            return string.IsNullOrWhiteSpace(extension) ? "url" : extension.TrimStart('.').ToLowerInvariant();
        }

        private static string GetFileNameFromUrl(Uri uri)
        {
            var name = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = uri.Host;
            }

            return name.Length > 255 ? name[..255] : name;
        }

        private static string NormalizeText(string input, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var text = mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                ? StripHtml(input)
                : input;

            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        private static string StripHtml(string html)
        {
            var withoutScript = Regex.Replace(
                html,
                "<script.*?</script>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var withoutStyle = Regex.Replace(
                withoutScript,
                "<style.*?</style>",
                " ",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            return Regex.Replace(withoutStyle, "<[^>]+>", " ");
        }

        private static string TrimToMax(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private static async Task<bool> IsBlockedHostAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IPAddress.TryParse(uri.Host, out var parsedIp))
            {
                return IsPrivateOrLocalIp(parsedIp);
            }

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
                return addresses.Any(IsPrivateOrLocalIp);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsPrivateOrLocalIp(IPAddress ipAddress)
        {
            if (IPAddress.IsLoopback(ipAddress))
            {
                return true;
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ipAddress.GetAddressBytes();
                return bytes[0] == 10 ||
                       bytes[0] == 127 ||
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                       (bytes[0] == 192 && bytes[1] == 168) ||
                       (bytes[0] == 169 && bytes[1] == 254);
            }

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ipAddress.IsIPv6LinkLocal ||
                       ipAddress.IsIPv6Multicast ||
                       ipAddress.IsIPv6SiteLocal ||
                       ipAddress.Equals(IPAddress.IPv6Loopback);
            }

            return false;
        }

        private sealed record FetchResult(
            string SourceType,
            string FileType,
            string FileName,
            string FetchStatus,
            string? FetchError,
            string ExtractedText,
            string Message);
    }
}
