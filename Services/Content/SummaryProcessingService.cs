using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Web_Project.Models;
using Web_Project.Services.AI;
using UglyToad.PdfPig;

namespace Web_Project.Services.Content
{
    public class SummaryProcessingService : ISummaryProcessingService
    {
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm"
        };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"
        };

        private const long MaxUploadBytes = 100L * 1024L * 1024L;

        private readonly IGeminiSummaryService _geminiSummaryService;
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SummaryProcessingService> _logger;

        public SummaryProcessingService(
            IGeminiSummaryService geminiSummaryService,
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<SummaryProcessingService> logger)
        {
            _geminiSummaryService = geminiSummaryService;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<SummarizeUploadResponse> SummarizeUploadAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file.Length <= 0)
            {
                throw new InvalidOperationException("File upload rỗng.");
            }

            if (file.Length > MaxUploadBytes)
            {
                throw new InvalidOperationException("File vượt giới hạn 100MB.");
            }

            var extension = NormalizeExtension(Path.GetExtension(file.FileName));
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new NotSupportedException("Không xác định được định dạng file.");
            }

            await using var source = file.OpenReadStream();
            await using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();

            return await SummarizeFromBytesAsync(
                fileName: file.FileName,
                extension: extension,
                mediaType: file.ContentType ?? string.Empty,
                charset: null,
                payload: bytes,
                sourceUrl: null,
                cancellationToken: cancellationToken);
        }

        public Task<SummarizeUploadResponse> SummarizeTextAsync(
            string text,
            string? sourceHint,
            CancellationToken cancellationToken)
        {
            var normalized = NormalizeText(text ?? string.Empty, ".txt", "text/plain");
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung văn bản rỗng.");
            }

            var inputType = string.IsNullOrWhiteSpace(sourceHint) ? "text" : sourceHint.Trim();
            if (inputType.Length > 64)
            {
                inputType = inputType[..64];
            }

            return BuildTextSummaryResponseAsync(
                fileName: "inline-text.txt",
                inputType: inputType,
                extractedText: normalized,
                usedVisionModel: false,
                usedTranscription: false,
                sourceUrl: null,
                cancellationToken: cancellationToken);
        }

        public async Task<SummarizeUrlResponse> SummarizeFromUrlAsync(
            string url,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("URL không hợp lệ. Chỉ chấp nhận http/https.");
            }

            if (await IsBlockedHostAsync(uri, cancellationToken))
            {
                throw new InvalidOperationException("URL không được phép truy cập vì lý do bảo mật.");
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(45);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("AI-Study-Summarizer/1.0");

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Không truy cập được URL: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            var charset = response.Content.Headers.ContentType?.CharSet;
            var fileName = ResolveFileNameFromUrl(uri, response.Content.Headers.ContentDisposition?.FileNameStar, response.Content.Headers.ContentDisposition?.FileName);
            var extension = NormalizeExtension(Path.GetExtension(fileName));

            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (payload.Length == 0)
            {
                throw new InvalidOperationException("URL trả về nội dung rỗng.");
            }

            if (payload.Length > MaxUploadBytes)
            {
                throw new InvalidOperationException("Nội dung URL vượt giới hạn 100MB.");
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = NormalizeExtension(Path.GetExtension(uri.AbsolutePath));
            }

            var summarized = await SummarizeFromBytesAsync(
                fileName: fileName,
                extension: extension,
                mediaType: mediaType,
                charset: charset,
                payload: payload,
                sourceUrl: url,
                cancellationToken: cancellationToken);

            return new SummarizeUrlResponse
            {
                ContentId = summarized.ContentId,
                Url = url,
                FileName = summarized.FileName,
                InputType = summarized.InputType,
                DetectedMimeType = string.IsNullOrWhiteSpace(mediaType) ? "unknown" : mediaType,
                ExtractedTextLength = summarized.ExtractedTextLength,
                UsedVisionModel = summarized.UsedVisionModel,
                UsedTranscription = summarized.UsedTranscription,
                Summary = summarized.Summary,
                KeyPoints = summarized.KeyPoints,
                Preview = summarized.Preview
            };
        }

        private async Task<SummarizeUploadResponse> SummarizeFromBytesAsync(
            string fileName,
            string extension,
            string mediaType,
            string? charset,
            byte[] payload,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            if (IsPdf(mediaType, extension))
            {
                return await SummarizePdfBytesAsync(fileName, payload, sourceUrl, cancellationToken);
            }

            if (IsDocx(mediaType, extension))
            {
                return await SummarizeDocxBytesAsync(fileName, payload, sourceUrl, cancellationToken);
            }

            if (IsImage(mediaType, extension))
            {
                return await SummarizeImageBytesAsync(fileName, extension, payload, sourceUrl, cancellationToken);
            }

            if (IsVideo(mediaType, extension))
            {
                return await SummarizeVideoBytesAsync(fileName, extension, payload, sourceUrl, cancellationToken);
            }

            if (IsTextLike(mediaType, extension))
            {
                var text = DecodeTextBytes(payload, charset);
                var normalized = NormalizeText(text, extension, mediaType);
                return await BuildTextSummaryResponseAsync(
                    fileName: fileName,
                    inputType: extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                               extension.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                               mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                        ? "webpage"
                        : "text",
                    extractedText: normalized,
                    usedVisionModel: false,
                    usedTranscription: false,
                    sourceUrl: sourceUrl,
                    cancellationToken: cancellationToken);
            }

            throw new NotSupportedException(
                "Định dạng chưa hỗ trợ. Hãy dùng link/file có nội dung text, html, pdf, docx, ảnh hoặc video trực tiếp.");
        }

        private async Task<SummarizeUploadResponse> SummarizePdfBytesAsync(
            string fileName,
            byte[] payload,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream(payload);
            var sb = new StringBuilder();

            using (var document = PdfDocument.Open(memory))
            {
                foreach (var page in document.GetPages())
                {
                    if (!string.IsNullOrWhiteSpace(page.Text))
                    {
                        sb.AppendLine(page.Text);
                    }
                }
            }

            var normalized = NormalizeText(sb.ToString(), ".txt", "text/plain");
            return await BuildTextSummaryResponseAsync(
                fileName: fileName,
                inputType: "pdf",
                extractedText: normalized,
                usedVisionModel: false,
                usedTranscription: false,
                sourceUrl: sourceUrl,
                cancellationToken: cancellationToken);
        }

        private async Task<SummarizeUploadResponse> SummarizeDocxBytesAsync(
            string fileName,
            byte[] payload,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream(payload);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: true);
            var documentEntry = archive.GetEntry("word/document.xml");
            if (documentEntry is null)
            {
                throw new InvalidOperationException("File DOCX không hợp lệ hoặc không có nội dung văn bản.");
            }

            using var entryStream = documentEntry.Open();
            var xml = XDocument.Load(entryStream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var paragraphs = xml
                .Descendants(w + "p")
                .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)))
                .Where(p => !string.IsNullOrWhiteSpace(p));

            var joined = string.Join(Environment.NewLine, paragraphs);
            var normalized = NormalizeText(joined, ".txt", "text/plain");
            return await BuildTextSummaryResponseAsync(
                fileName: fileName,
                inputType: "docx",
                extractedText: normalized,
                usedVisionModel: false,
                usedTranscription: false,
                sourceUrl: sourceUrl,
                cancellationToken: cancellationToken);
        }

        private async Task<SummarizeUploadResponse> SummarizeImageBytesAsync(
            string fileName,
            string extension,
            byte[] payload,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            var startedAt = Stopwatch.StartNew();
            var mimeType = GetImageMimeType(extension);
            var result = await _geminiSummaryService.SummarizeImageAsync(
                imageBytes: payload,
                mimeType: mimeType,
                fileName: fileName,
                cancellationToken: cancellationToken);
            startedAt.Stop();

            var generatedFileName = BuildMeaningfulFileName(result.Summary, "image", fileName);
            var contentId = await SaveSummaryRecordAsync(
                generatedFileName: generatedFileName,
                inputType: "image",
                originalFileName: fileName,
                sourceUrl: sourceUrl,
                extractedText: result.Summary,
                summary: result.Summary,
                keyPoints: result.KeyPoints,
                processingTimeSeconds: startedAt.Elapsed.TotalSeconds,
                cancellationToken: cancellationToken);

            return new SummarizeUploadResponse
            {
                ContentId = contentId,
                FileName = generatedFileName,
                InputType = "image",
                ExtractedTextLength = result.Summary.Length,
                UsedVisionModel = true,
                UsedTranscription = false,
                Summary = result.Summary,
                KeyPoints = result.KeyPoints,
                Preview = BuildPreview(result.Summary)
            };
        }

        private async Task<SummarizeUploadResponse> SummarizeVideoBytesAsync(
            string fileName,
            string extension,
            byte[] payload,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".mp4" : extension;
            var videoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{safeExtension}");
            var audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp3");

            try
            {
                await File.WriteAllBytesAsync(videoPath, payload, cancellationToken);
                await ExtractAudioFromVideoAsync(videoPath, audioPath, cancellationToken);

                var transcript = await _geminiSummaryService.TranscribeAudioAsync(audioPath, cancellationToken);
                var normalized = NormalizeText(transcript, ".txt", "text/plain");
                return await BuildTextSummaryResponseAsync(
                    fileName: fileName,
                    inputType: "video",
                    extractedText: normalized,
                    usedVisionModel: false,
                    usedTranscription: true,
                    sourceUrl: sourceUrl,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                SafeDeleteFile(videoPath);
                SafeDeleteFile(audioPath);
            }
        }

        private async Task<SummarizeUploadResponse> BuildTextSummaryResponseAsync(
            string fileName,
            string inputType,
            string extractedText,
            bool usedVisionModel,
            bool usedTranscription,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Không trích xuất được văn bản để tóm tắt.");
            }

            var startedAt = Stopwatch.StartNew();
            var summary = await _geminiSummaryService.SummarizeTextAsync(
                text: extractedText,
                sourceHint: inputType,
                cancellationToken: cancellationToken);
            startedAt.Stop();

            var generatedFileName = BuildMeaningfulFileName(summary.Summary, inputType, fileName);
            var contentId = await SaveSummaryRecordAsync(
                generatedFileName: generatedFileName,
                inputType: inputType,
                originalFileName: fileName,
                sourceUrl: sourceUrl,
                extractedText: extractedText,
                summary: summary.Summary,
                keyPoints: summary.KeyPoints,
                processingTimeSeconds: startedAt.Elapsed.TotalSeconds,
                cancellationToken: cancellationToken);

            return new SummarizeUploadResponse
            {
                ContentId = contentId,
                FileName = generatedFileName,
                InputType = inputType,
                ExtractedTextLength = extractedText.Length,
                UsedVisionModel = usedVisionModel,
                UsedTranscription = usedTranscription,
                Summary = summary.Summary,
                KeyPoints = summary.KeyPoints,
                Preview = BuildPreview(extractedText)
            };
        }

        private static string NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            return extension.Trim().ToLowerInvariant();
        }

        private static bool IsPdf(string mediaType, string extension)
        {
            return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDocx(string mediaType, string extension)
        {
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(string mediaType, string extension)
        {
            return mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   ImageExtensions.Contains(extension);
        }

        private static bool IsVideo(string mediaType, string extension)
        {
            return mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                   VideoExtensions.Contains(extension);
        }

        private static bool IsTextLike(string mediaType, string extension)
        {
            if (TextExtensions.Contains(extension))
            {
                return true;
            }

            if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("html", StringComparison.OrdinalIgnoreCase);
        }

        private static string DecodeTextBytes(byte[] payload, string? charset)
        {
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try
                {
                    var encoding = Encoding.GetEncoding(charset.Trim('"'));
                    return encoding.GetString(payload);
                }
                catch
                {
                    // Fallback to UTF-8.
                }
            }

            return Encoding.UTF8.GetString(payload);
        }

        private static string NormalizeText(string input, string extension, string mediaType)
        {
            var text = input ?? string.Empty;
            if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                text = Regex.Replace(text, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, "<[^>]+>", " ");
                text = WebUtility.HtmlDecode(text);
            }

            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            text = Regex.Replace(text, "[ \t]+", " ");
            text = Regex.Replace(text, "\n{3,}", "\n\n");
            return text.Trim();
        }

        private static string BuildPreview(string text)
        {
            const int maxPreviewChars = 600;
            if (text.Length <= maxPreviewChars)
            {
                return text;
            }

            return $"{text[..maxPreviewChars]}...";
        }

        private async Task<int> SaveSummaryRecordAsync(
            string generatedFileName,
            string inputType,
            string originalFileName,
            string? sourceUrl,
            string extractedText,
            string summary,
            List<string> keyPoints,
            double processingTimeSeconds,
            CancellationToken cancellationToken)
        {
            var sourceType = ResolveSourceTypeForPersistence(inputType, sourceUrl);
            var createdAt = DateTime.UtcNow;

            var content = new Web_Project.Models.Content
            {
                UserId = null,
                IsGuest = true,
                FileName = generatedFileName,
                FileType = ResolveFileTypeForPersistence(inputType, originalFileName),
                FilePath = string.IsNullOrWhiteSpace(sourceUrl) ? originalFileName : sourceUrl,
                SourceType = sourceType,
                SourceUrl = sourceType == "FileUpload" ? null : sourceUrl,
                FetchStatus = sourceType == "FileUpload" ? null : "Completed",
                FetchError = null,
                ExtractedText = TrimToMax(extractedText, 500_000),
                AI_DetectedSubject = BuildDetectedSubject(summary),
                AI_DetectedGrade = string.Empty,
                CreatedAt = createdAt,
                AIProcess = new AIProcess
                {
                    Summary = summary,
                    KeyPoints = SerializeKeyPoints(keyPoints),
                    ProcessingTime = processingTimeSeconds,
                    CreatedAt = createdAt
                }
            };

            _dbContext.Contents.Add(content);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return content.ContentId;
        }

        private static string ResolveSourceTypeForPersistence(string inputType, string? sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return "FileUpload";
            }

            var normalizedType = (inputType ?? string.Empty).Trim().ToLowerInvariant();
            return normalizedType switch
            {
                "text" or "webpage" => "TextUrl",
                "video" => "VideoUrl",
                _ => "DocumentUrl"
            };
        }

        private static string ResolveFileTypeForPersistence(string inputType, string originalFileName)
        {
            var normalizedType = (inputType ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedType))
            {
                return normalizedType;
            }

            var extension = NormalizeExtension(Path.GetExtension(originalFileName));
            return string.IsNullOrWhiteSpace(extension) ? "unknown" : extension.TrimStart('.');
        }

        private static string BuildDetectedSubject(string summary)
        {
            var normalized = NormalizeText(summary ?? string.Empty, ".txt", "text/plain");
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            const int maxLength = 200;
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static string SerializeKeyPoints(List<string> keyPoints)
        {
            var sanitized = keyPoints
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            return JsonSerializer.Serialize(sanitized);
        }

        private static string BuildMeaningfulFileName(string summary, string inputType, string originalFileName)
        {
            var stem = BuildStemFromSummary(summary);
            if (string.IsNullOrWhiteSpace(stem))
            {
                var normalizedType = BuildStemFromSummary(inputType);
                stem = string.IsNullOrWhiteSpace(normalizedType) ? "summary" : $"{normalizedType}-summary";
            }

            const int maxStemLength = 72;
            if (stem.Length > maxStemLength)
            {
                stem = stem[..maxStemLength].Trim('-');
            }

            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "summary";
            }

            var extension = ResolveOutputExtension(inputType, originalFileName);
            return $"{stem}{extension}";
        }

        private static string ResolveOutputExtension(string inputType, string originalFileName)
        {
            var normalizedType = (inputType ?? string.Empty).Trim().ToLowerInvariant();
            var originalExtension = NormalizeExtension(Path.GetExtension(originalFileName));

            return normalizedType switch
            {
                "pdf" => ".pdf",
                "docx" => ".docx",
                "image" => ImageExtensions.Contains(originalExtension) ? originalExtension : ".png",
                "video" => ".mp4",
                _ => ".txt"
            };
        }

        private static string BuildStemFromSummary(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var text = RemoveDiacritics(raw).Replace('đ', 'd').Replace('Đ', 'D');
            text = text.ToLowerInvariant();
            text = Regex.Replace(text, @"[^a-z0-9]+", " ");

            var words = text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length >= 2)
                .Take(10)
                .ToArray();

            if (words.Length == 0)
            {
                words = text
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(10)
                    .ToArray();
            }

            return string.Join("-", words).Trim('-');
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string TrimToMax(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string GetImageMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private static string ResolveFileNameFromUrl(Uri uri, string? fileNameStar, string? fileName)
        {
            var fromHeader = string.IsNullOrWhiteSpace(fileNameStar) ? fileName : fileNameStar;
            if (!string.IsNullOrWhiteSpace(fromHeader))
            {
                var cleaned = fromHeader.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned.Length > 255 ? cleaned[..255] : cleaned;
                }
            }

            var fromPath = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(fromPath))
            {
                return fromPath.Length > 255 ? fromPath[..255] : fromPath;
            }

            return uri.Host;
        }

        private async Task ExtractAudioFromVideoAsync(
            string videoPath,
            string audioPath,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{videoPath}\" -vn -ac 1 -ar 16000 -f mp3 \"{audioPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    throw new InvalidOperationException("Không khởi tạo được ffmpeg để xử lý video.");
                }

                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                var stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("ffmpeg failed with code {ExitCode}: {Error}", process.ExitCode, stderr);
                    throw new InvalidOperationException("Không thể trích xuất âm thanh từ video.");
                }
            }
            catch (Win32Exception)
            {
                throw new InvalidOperationException(
                    "Máy chủ chưa cài ffmpeg. Vui lòng cài ffmpeg để hỗ trợ tóm tắt video.");
            }
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

        private static void SafeDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Intentionally ignore cleanup errors.
            }
        }
    }
}
