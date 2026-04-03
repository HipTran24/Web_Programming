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
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Services.AI;
using Web_Project.Services.Notifications;
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

        private static readonly CultureInfo VietnameseCulture = new("vi-VN");

        private const long MaxUploadBytes = 100L * 1024L * 1024L;
        private const int AiChunkTargetCharacters = 12_000;
        private const int AiChunkMinWindowCharacters = 4_500;
        private const int AiMergeTargetCharacters = 14_000;
        private static readonly Regex YoutubeWatchIdRegex = new(@"[?&]v=([A-Za-z0-9_-]{11})", RegexOptions.Compiled);
        private static readonly Regex YoutubeShortIdRegex = new(@"^/([A-Za-z0-9_-]{11})(?:[/?#]|$)", RegexOptions.Compiled);

        private readonly IGroqSummaryService _groqSummaryService;
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IContentSafetyService _contentSafetyService;
        private readonly ILogger<SummaryProcessingService> _logger;
        private readonly ISystemNotificationService? _systemNotificationService;

        public SummaryProcessingService(
            IGroqSummaryService groqSummaryService,
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IContentSafetyService contentSafetyService,
            ILogger<SummaryProcessingService> logger,
            ISystemNotificationService? systemNotificationService = null)
        {
            _groqSummaryService = groqSummaryService;
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _contentSafetyService = contentSafetyService;
            _logger = logger;
            _systemNotificationService = systemNotificationService;
        }

        public async Task<SummarizeUploadResponse> SummarizeUploadAsync(
            IFormFile file,
            int? userId,
            bool isGuest,
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
                userId: userId,
                isGuest: isGuest,
                cancellationToken: cancellationToken);
        }

        public Task<SummarizeUploadResponse> SummarizeTextAsync(
            string text,
            string? sourceHint,
            int? userId,
            bool isGuest,
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
                userId: userId,
                isGuest: isGuest,
                cancellationToken: cancellationToken);
        }

        public async Task<SummarizeUrlResponse> SummarizeFromUrlAsync(
            string url,
            int? userId,
            bool isGuest,
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

            if (IsYouTubeUrl(uri))
            {
                return await SummarizeYouTubeUrlAsync(uri, url, userId, isGuest, cancellationToken);
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
                userId: userId,
                isGuest: isGuest,
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
                Preview = summarized.Preview,
                RequiresAdminReview = summarized.RequiresAdminReview,
                ModerationStatus = summarized.ModerationStatus,
                ModerationMessage = summarized.ModerationMessage,
                ModerationFlags = summarized.ModerationFlags
            };
        }

        private async Task<SummarizeUrlResponse> SummarizeYouTubeUrlAsync(
            Uri uri,
            string originalUrl,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            var videoId = TryExtractYouTubeVideoId(uri);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new InvalidOperationException("Không nhận diện được video YouTube hợp lệ từ URL đã nhập.");
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(45);

            var metadata = await FetchYouTubeMetadataAsync(client, uri, videoId, cancellationToken);

            var normalizedText = NormalizeText(metadata.BuildExtractionText(), ".txt", "text/plain");
            if (string.IsNullOrWhiteSpace(normalizedText) || normalizedText.Length < 40)
            {
                throw new InvalidOperationException(
                    "Không lấy được đủ dữ liệu từ video YouTube. Hãy thử video có mô tả/phụ đề hoặc tải file video trực tiếp.");
            }

            var fileName = !string.IsNullOrWhiteSpace(metadata.Title)
                ? $"{BuildStemFromSummary(metadata.Title)}.txt"
                : $"youtube-{videoId}.txt";

            var summarized = await BuildTextSummaryResponseAsync(
                fileName: fileName,
                inputType: "video",
                extractedText: normalizedText,
                usedVisionModel: false,
                usedTranscription: metadata.HasTranscript,
                sourceUrl: originalUrl,
                userId: userId,
                isGuest: isGuest,
                cancellationToken: cancellationToken);

            return new SummarizeUrlResponse
            {
                ContentId = summarized.ContentId,
                Url = originalUrl,
                FileName = summarized.FileName,
                InputType = summarized.InputType,
                DetectedMimeType = "video/youtube",
                ExtractedTextLength = summarized.ExtractedTextLength,
                UsedVisionModel = summarized.UsedVisionModel,
                UsedTranscription = summarized.UsedTranscription,
                Summary = summarized.Summary,
                KeyPoints = summarized.KeyPoints,
                Preview = summarized.Preview,
                RequiresAdminReview = summarized.RequiresAdminReview,
                ModerationStatus = summarized.ModerationStatus,
                ModerationMessage = summarized.ModerationMessage,
                ModerationFlags = summarized.ModerationFlags
            };
        }

        private static bool IsYouTubeUrl(Uri uri)
        {
            var host = uri.Host.ToLowerInvariant();
            return host == "youtube.com" ||
                   host == "www.youtube.com" ||
                   host == "m.youtube.com" ||
                   host == "youtu.be";
        }

        private static string TryExtractYouTubeVideoId(Uri uri)
        {
            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath;

            if (host == "youtu.be")
            {
                var shortMatch = YoutubeShortIdRegex.Match(path);
                return shortMatch.Success ? shortMatch.Groups[1].Value : string.Empty;
            }

            if (path.Equals("/watch", StringComparison.OrdinalIgnoreCase))
            {
                var watchMatch = YoutubeWatchIdRegex.Match(uri.Query);
                return watchMatch.Success ? watchMatch.Groups[1].Value : string.Empty;
            }

            if (path.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/live/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && segments[1].Length == 11)
                {
                    return segments[1];
                }
            }

            return string.Empty;
        }

        private async Task<YouTubeMetadata> FetchYouTubeMetadataAsync(
            HttpClient client,
            Uri originalUri,
            string videoId,
            CancellationToken cancellationToken)
        {
            var metadata = new YouTubeMetadata
            {
                VideoId = videoId,
                VideoUrl = originalUri.ToString()
            };

            try
            {
                var oembedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(metadata.VideoUrl)}&format=json";
                using var oembedResp = await client.GetAsync(oembedUrl, cancellationToken);
                if (oembedResp.IsSuccessStatusCode)
                {
                    var oembedBody = await oembedResp.Content.ReadAsStringAsync(cancellationToken);
                    using var oembedDoc = JsonDocument.Parse(oembedBody);
                    if (oembedDoc.RootElement.TryGetProperty("title", out var titleElement))
                    {
                        metadata.Title = titleElement.GetString() ?? string.Empty;
                    }

                    if (oembedDoc.RootElement.TryGetProperty("author_name", out var authorElement))
                    {
                        metadata.Channel = authorElement.GetString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được YouTube oEmbed cho video {VideoId}", videoId);
            }

            var watchUrl = $"https://www.youtube.com/watch?v={videoId}&hl=vi";
            string html;
            try
            {
                using var watchReq = new HttpRequestMessage(HttpMethod.Get, watchUrl);
                watchReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AI-Study-Summarizer/1.0)");
                using var watchResp = await client.SendAsync(watchReq, cancellationToken);
                if (!watchResp.IsSuccessStatusCode)
                {
                    return metadata;
                }

                html = await watchResp.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được YouTube watch page cho video {VideoId}", videoId);
                return metadata;
            }

            metadata.Description = TryExtractJsonStringValue(html, "shortDescription");
            if (string.IsNullOrWhiteSpace(metadata.Title))
            {
                metadata.Title = TryExtractJsonStringValue(html, "title");
            }

            var transcriptUrl = TryExtractCaptionUrl(html);
            if (string.IsNullOrWhiteSpace(transcriptUrl))
            {
                return metadata;
            }

            try
            {
                var transcriptText = await FetchYouTubeTranscriptAsync(client, transcriptUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(transcriptText))
                {
                    metadata.Transcript = transcriptText;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không đọc được phụ đề YouTube cho video {VideoId}", videoId);
            }

            return metadata;
        }

        private static string TryExtractJsonStringValue(string html, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var match = Regex.Match(
                html,
                $"\\\"{Regex.Escape(fieldName)}\\\":\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return string.Empty;
            }

            return DecodeJsonEscapedString(match.Groups["value"].Value);
        }

        private static string TryExtractCaptionUrl(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var captionTracksMatch = Regex.Match(
                html,
                "\"captionTracks\":\\[(?<tracks>.*?)\\]",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (!captionTracksMatch.Success)
            {
                return string.Empty;
            }

            var tracks = captionTracksMatch.Groups["tracks"].Value;
            var baseUrlMatch = Regex.Match(
                tracks,
                "\"baseUrl\":\"(?<url>(?:\\\\.|[^\"])*)\"",
                RegexOptions.IgnoreCase);

            if (!baseUrlMatch.Success)
            {
                return string.Empty;
            }

            var decoded = DecodeJsonEscapedString(baseUrlMatch.Groups["url"].Value);
            return WebUtility.HtmlDecode(decoded);
        }

        private static async Task<string> FetchYouTubeTranscriptAsync(
            HttpClient client,
            string transcriptUrl,
            CancellationToken cancellationToken)
        {
            var normalizedUrl = transcriptUrl.Contains("fmt=", StringComparison.OrdinalIgnoreCase)
                ? transcriptUrl
                : $"{transcriptUrl}&fmt=srv3";

            using var captionResp = await client.GetAsync(normalizedUrl, cancellationToken);
            if (!captionResp.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var captionBody = await captionResp.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(captionBody))
            {
                return string.Empty;
            }

            try
            {
                var xml = XDocument.Parse(captionBody);
                var lines = xml.Descendants("text")
                    .Select(x => WebUtility.HtmlDecode(x.Value))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => Regex.Replace(x.Trim(), "\\s+", " "))
                    .ToList();

                return string.Join(" ", lines);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string DecodeJsonEscapedString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            try
            {
                return JsonSerializer.Deserialize<string>($"\"{input}\"") ?? string.Empty;
            }
            catch
            {
                return input;
            }
        }

        private sealed class YouTubeMetadata
        {
            public string VideoId { get; set; } = string.Empty;

            public string VideoUrl { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string Channel { get; set; } = string.Empty;

            public string Description { get; set; } = string.Empty;

            public string Transcript { get; set; } = string.Empty;

            public bool HasTranscript => !string.IsNullOrWhiteSpace(Transcript);

            public string BuildExtractionText()
            {
                var sb = new StringBuilder();
                sb.AppendLine("Nguon: YouTube");
                if (!string.IsNullOrWhiteSpace(VideoUrl))
                {
                    sb.AppendLine($"URL: {VideoUrl}");
                }

                if (!string.IsNullOrWhiteSpace(Title))
                {
                    sb.AppendLine($"Tieu de: {Title}");
                }

                if (!string.IsNullOrWhiteSpace(Channel))
                {
                    sb.AppendLine($"Kenh: {Channel}");
                }

                if (!string.IsNullOrWhiteSpace(Description))
                {
                    sb.AppendLine();
                    sb.AppendLine("Mo ta video:");
                    sb.AppendLine(Description);
                }

                if (!string.IsNullOrWhiteSpace(Transcript))
                {
                    sb.AppendLine();
                    sb.AppendLine("Phu de / Loi thoai tu video:");
                    sb.AppendLine(Transcript);
                }

                if (sb.Length < 80)
                {
                    sb.AppendLine();
                    sb.AppendLine("Khong tim thay du metadata/phu de tu YouTube, nen tom tat co the khong day du.");
                }

                return sb.ToString();
            }
        }

        private async Task<SummarizeUploadResponse> SummarizeFromBytesAsync(
            string fileName,
            string extension,
            string mediaType,
            string? charset,
            byte[] payload,
            string? sourceUrl,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            if (IsPdf(mediaType, extension))
            {
                return await SummarizePdfBytesAsync(fileName, payload, sourceUrl, userId, isGuest, cancellationToken);
            }

            if (IsDocx(mediaType, extension))
            {
                return await SummarizeDocxBytesAsync(fileName, payload, sourceUrl, userId, isGuest, cancellationToken);
            }

            if (IsImage(mediaType, extension))
            {
                return await SummarizeImageBytesAsync(fileName, extension, payload, sourceUrl, userId, isGuest, cancellationToken);
            }

            if (IsVideo(mediaType, extension))
            {
                return await SummarizeVideoBytesAsync(fileName, extension, payload, sourceUrl, userId, isGuest, cancellationToken);
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
                    userId: userId,
                    isGuest: isGuest,
                    cancellationToken: cancellationToken);
            }

            throw new NotSupportedException(
                "Định dạng chưa hỗ trợ. Hãy dùng link/file có nội dung text, html, pdf, docx, ảnh hoặc video trực tiếp.");
        }

        private async Task<SummarizeUploadResponse> SummarizePdfBytesAsync(
            string fileName,
            byte[] payload,
            string? sourceUrl,
            int? userId,
            bool isGuest,
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
                userId: userId,
                isGuest: isGuest,
                cancellationToken: cancellationToken);
        }

        private async Task<SummarizeUploadResponse> SummarizeDocxBytesAsync(
            string fileName,
            byte[] payload,
            string? sourceUrl,
            int? userId,
            bool isGuest,
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
                userId: userId,
                isGuest: isGuest,
                cancellationToken: cancellationToken);
        }

        private async Task<SummarizeUploadResponse> SummarizeImageBytesAsync(
            string fileName,
            string extension,
            byte[] payload,
            string? sourceUrl,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            var startedAt = Stopwatch.StartNew();
            try
            {
                var mimeType = GetImageMimeType(extension);
                var result = await _groqSummaryService.SummarizeImageAsync(
                    imageBytes: payload,
                    mimeType: mimeType,
                    fileName: fileName,
                    cancellationToken: cancellationToken);
                startedAt.Stop();

                var safetyReview = await _contentSafetyService.AnalyzeAsync(
                    extractedText: result.Summary,
                    summary: string.Empty,
                    keyPoints: Array.Empty<string>(),
                    fileName: fileName,
                    sourceUrl: sourceUrl,
                    cancellationToken: cancellationToken);

                var generatedFileName = safetyReview.BlocksSummarization
                    ? ResolveStoredFileNameForBlockedContent(fileName, "image")
                    : BuildMeaningfulFileName(result.Summary, "image", fileName);
                var savedRecord = await SaveSummaryRecordAsync(
                    generatedFileName: generatedFileName,
                    inputType: "image",
                    originalFileName: fileName,
                    sourceUrl: sourceUrl,
                    extractedText: result.Summary,
                    summary: safetyReview.BlocksSummarization ? string.Empty : result.Summary,
                    keyPoints: safetyReview.BlocksSummarization ? [] : result.KeyPoints,
                    processingTimeSeconds: startedAt.Elapsed.TotalSeconds,
                    userId: userId,
                    isGuest: isGuest,
                    safetyReview: safetyReview,
                    cancellationToken: cancellationToken);

                await PersistAiLogAsync("Summary.Image", userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: false, cancellationToken);

                return new SummarizeUploadResponse
                {
                    ContentId = savedRecord.ContentId,
                    FileName = generatedFileName,
                    InputType = "image",
                    ExtractedTextLength = result.Summary.Length,
                    UsedVisionModel = true,
                    UsedTranscription = false,
                    Summary = safetyReview.BlocksSummarization ? string.Empty : result.Summary,
                    KeyPoints = safetyReview.BlocksSummarization ? [] : result.KeyPoints,
                    Preview = BuildPreview(result.Summary),
                    RequiresAdminReview = savedRecord.SafetyReview.RequiresAdminReview,
                    ModerationStatus = savedRecord.SafetyReview.ModerationStatus,
                    ModerationMessage = savedRecord.SafetyReview.WarningMessage,
                    ModerationFlags = savedRecord.SafetyReview.Flags
                };
            }
            catch
            {
                startedAt.Stop();
                await PersistAiLogAsync("Summary.Image", userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: true, cancellationToken);
                throw;
            }
        }

        private async Task<SummarizeUploadResponse> SummarizeVideoBytesAsync(
            string fileName,
            string extension,
            byte[] payload,
            string? sourceUrl,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".mp4" : extension;
            var videoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{safeExtension}");
            var audioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp3");

            try
            {
                await File.WriteAllBytesAsync(videoPath, payload, cancellationToken);
                await ExtractAudioFromVideoAsync(videoPath, audioPath, cancellationToken);

                var transcript = await _groqSummaryService.TranscribeAudioAsync(audioPath, cancellationToken);
                var normalized = NormalizeText(transcript, ".txt", "text/plain");
                return await BuildTextSummaryResponseAsync(
                    fileName: fileName,
                    inputType: "video",
                    extractedText: normalized,
                    usedVisionModel: false,
                    usedTranscription: true,
                    sourceUrl: sourceUrl,
                    userId: userId,
                    isGuest: isGuest,
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
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("Không trích xuất được văn bản để tóm tắt.");
            }

            var startedAt = Stopwatch.StartNew();
            try
            {
                var safetyReview = await _contentSafetyService.AnalyzeAsync(
                    extractedText: extractedText,
                    summary: string.Empty,
                    keyPoints: Array.Empty<string>(),
                    fileName: fileName,
                    sourceUrl: sourceUrl,
                    cancellationToken: cancellationToken);

                if (safetyReview.BlocksSummarization)
                {
                    startedAt.Stop();

                    var storedFileName = ResolveStoredFileNameForBlockedContent(fileName, inputType);
                    var blockedRecord = await SaveSummaryRecordAsync(
                        generatedFileName: storedFileName,
                        inputType: inputType,
                        originalFileName: fileName,
                        sourceUrl: sourceUrl,
                        extractedText: extractedText,
                        summary: string.Empty,
                        keyPoints: [],
                        processingTimeSeconds: startedAt.Elapsed.TotalSeconds,
                        userId: userId,
                        isGuest: isGuest,
                        safetyReview: safetyReview,
                        cancellationToken: cancellationToken);

                    await PersistAiLogAsync(BuildSummaryActionType(inputType, usedVisionModel, usedTranscription), userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: false, cancellationToken);

                    return new SummarizeUploadResponse
                    {
                        ContentId = blockedRecord.ContentId,
                        FileName = storedFileName,
                        InputType = inputType,
                        ExtractedTextLength = extractedText.Length,
                        UsedVisionModel = usedVisionModel,
                        UsedTranscription = usedTranscription,
                        Summary = string.Empty,
                        KeyPoints = [],
                        Preview = BuildPreview(extractedText),
                        RequiresAdminReview = blockedRecord.SafetyReview.RequiresAdminReview,
                        ModerationStatus = blockedRecord.SafetyReview.ModerationStatus,
                        ModerationMessage = blockedRecord.SafetyReview.WarningMessage,
                        ModerationFlags = blockedRecord.SafetyReview.Flags
                    };
                }

                var summary = await GenerateSummaryPayloadAsync(extractedText, inputType, cancellationToken);
                startedAt.Stop();

                var generatedFileName = BuildMeaningfulFileName(summary.Summary, inputType, fileName);
                var savedRecord = await SaveSummaryRecordAsync(
                    generatedFileName: generatedFileName,
                    inputType: inputType,
                    originalFileName: fileName,
                    sourceUrl: sourceUrl,
                    extractedText: extractedText,
                    summary: summary.Summary,
                    keyPoints: summary.KeyPoints,
                    processingTimeSeconds: startedAt.Elapsed.TotalSeconds,
                    userId: userId,
                    isGuest: isGuest,
                    safetyReview: safetyReview,
                    cancellationToken: cancellationToken);

                await PersistAiLogAsync(BuildSummaryActionType(inputType, usedVisionModel, usedTranscription), userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: false, cancellationToken);

                return new SummarizeUploadResponse
                {
                    ContentId = savedRecord.ContentId,
                    FileName = generatedFileName,
                    InputType = inputType,
                    ExtractedTextLength = extractedText.Length,
                    UsedVisionModel = usedVisionModel,
                    UsedTranscription = usedTranscription,
                    Summary = summary.Summary,
                    KeyPoints = summary.KeyPoints,
                    Preview = BuildPreview(extractedText),
                    RequiresAdminReview = savedRecord.SafetyReview.RequiresAdminReview,
                    ModerationStatus = savedRecord.SafetyReview.ModerationStatus,
                    ModerationMessage = savedRecord.SafetyReview.WarningMessage,
                    ModerationFlags = savedRecord.SafetyReview.Flags
                };
            }
            catch
            {
                startedAt.Stop();
                await PersistAiLogAsync(BuildSummaryActionType(inputType, usedVisionModel, usedTranscription), userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: true, cancellationToken);
                throw;
            }
        }

        private static bool IsAiUnavailable(string? message)
        {
            var normalized = (message ?? string.Empty).ToLowerInvariant();
            return normalized.Contains("quá tải") ||
                   normalized.Contains("giới hạn") ||
                   normalized.Contains("vượt ngưỡng request") ||
                   normalized.Contains("context length") ||
                   normalized.Contains("maximum context") ||
                   normalized.Contains("too large") ||
                   normalized.Contains("quota") ||
                   normalized.Contains("resource_exhausted") ||
                   normalized.Contains("tạm thời") ||
                   normalized.Contains("try lại") ||
                   normalized.Contains("thử lại");
        }

        private static AiSummaryResult BuildLocalFallbackSummary(string extractedText, string inputType)
        {
            var clean = NormalizeText(extractedText, ".txt", "text/plain");
            if (string.IsNullOrWhiteSpace(clean))
            {
                return new AiSummaryResult
                {
                    Summary = "Nội dung đã được tải lên thành công, nhưng hệ thống AI đang bận nên chưa thể tạo tóm tắt chi tiết ngay lúc này.",
                    KeyPoints =
                    [
                        "Tệp đã được lưu thành công.",
                        "Bạn có thể thử tạo lại tóm tắt sau vài phút.",
                        "Hệ thống đang dùng chế độ dự phòng do AI tạm thời quá tải."
                    ]
                };
            }

            var sentences = clean
                .Split([".", "!", "?", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 20)
                .ToList();

            var topSentences = sentences.Take(4).ToList();
            if (topSentences.Count == 0)
            {
                topSentences.Add(clean[..Math.Min(clean.Length, 280)]);
            }

            var summaryText = string.Join(". ", topSentences);
            if (!summaryText.EndsWith(".", StringComparison.Ordinal))
            {
                summaryText += ".";
            }

            var keyPoints = topSentences
                .Take(5)
                .Select(x => x.Length > 180 ? x[..180].TrimEnd() + "..." : x)
                .ToList();

            keyPoints.Add($"Chế độ dự phòng được bật cho nguồn: {inputType}.");

            return new AiSummaryResult
            {
                Summary = summaryText,
                KeyPoints = keyPoints
            };
        }

        private async Task<AiSummaryResult> SummarizeTextWithChunkingAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            var normalized = (text ?? string.Empty).Trim();
            if (normalized.Length <= AiChunkTargetCharacters)
            {
                return await _groqSummaryService.SummarizeTextAsync(normalized, sourceHint, cancellationToken);
            }

            var chunks = SplitTextIntoAiChunks(normalized, AiChunkTargetCharacters, AiChunkMinWindowCharacters);
            if (chunks.Count <= 1)
            {
                return await _groqSummaryService.SummarizeTextAsync(normalized, sourceHint, cancellationToken);
            }

            _logger.LogInformation(
                "Split long content into {ChunkCount} AI chunks for source {SourceHint}. Original length: {Length}",
                chunks.Count,
                sourceHint,
                normalized.Length);

            var partials = new List<AiSummaryResult>(chunks.Count);
            foreach (var chunk in chunks)
            {
                partials.Add(await _groqSummaryService.SummarizeTextAsync(chunk, sourceHint, cancellationToken));
            }

            return await MergeChunkSummariesAsync(partials, sourceHint, cancellationToken);
        }

        private async Task<AiSummaryResult> MergeChunkSummariesAsync(
            IReadOnlyList<AiSummaryResult> partials,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            var packets = partials
                .Select((partial, index) => FormatSummaryPacket(partial, index + 1))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (packets.Count == 0)
            {
                throw new InvalidOperationException("Không tạo được dữ liệu trung gian để hợp nhất bản tóm tắt.");
            }

            while (packets.Count > 1 || packets[0].Length > AiMergeTargetCharacters)
            {
                var nextPackets = new List<string>();
                var groups = GroupPacketsForMerge(packets, AiMergeTargetCharacters);

                foreach (var group in groups)
                {
                    var mergeInput = BuildMergeSourceText(group);
                    var merged = await _groqSummaryService.SummarizeTextAsync(mergeInput, sourceHint, cancellationToken);
                    nextPackets.Add(FormatSummaryPacket(merged, nextPackets.Count + 1));
                }

                packets = nextPackets;
            }

            return await _groqSummaryService.SummarizeTextAsync(BuildMergeSourceText(packets), sourceHint, cancellationToken);
        }

        private static List<string> SplitTextIntoAiChunks(string text, int targetChars, int minWindowChars)
        {
            var chunks = new List<string>();
            var safeTarget = Math.Max(4_000, targetChars);
            var safeMinWindow = Math.Clamp(minWindowChars, 2_000, safeTarget);
            var index = 0;

            while (index < text.Length)
            {
                var remaining = text.Length - index;
                if (remaining <= safeTarget)
                {
                    chunks.Add(text[index..].Trim());
                    break;
                }

                var maxEnd = Math.Min(text.Length, index + safeTarget);
                var minEnd = Math.Min(text.Length, index + safeMinWindow);
                var end = FindChunkBoundary(text, minEnd, maxEnd);
                if (end <= index)
                {
                    end = maxEnd;
                }

                var chunk = text[index..end].Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                index = SkipWhitespace(text, end);
            }

            return chunks;
        }

        private static int FindChunkBoundary(string text, int minEnd, int maxEnd)
        {
            if (maxEnd <= minEnd)
            {
                return maxEnd;
            }

            var window = text[minEnd..maxEnd];
            foreach (var separator in new[] { "\n\n", "\n", ". ", "! ", "? ", "; ", ", ", " " })
            {
                var relative = window.LastIndexOf(separator, StringComparison.Ordinal);
                if (relative >= 0)
                {
                    return minEnd + relative + separator.Length;
                }
            }

            return maxEnd;
        }

        private static int SkipWhitespace(string text, int index)
        {
            var current = Math.Clamp(index, 0, text.Length);
            while (current < text.Length && char.IsWhiteSpace(text[current]))
            {
                current++;
            }

            return current;
        }

        private static List<List<string>> GroupPacketsForMerge(IReadOnlyList<string> packets, int targetChars)
        {
            var groups = new List<List<string>>();
            var currentGroup = new List<string>();
            var currentLength = 0;

            foreach (var packet in packets)
            {
                var packetLength = packet.Length;
                if (currentGroup.Count > 0 && currentLength + packetLength > targetChars)
                {
                    groups.Add(currentGroup);
                    currentGroup = [];
                    currentLength = 0;
                }

                currentGroup.Add(packet);
                currentLength += packetLength;
            }

            if (currentGroup.Count > 0)
            {
                groups.Add(currentGroup);
            }

            return groups;
        }

        private static string BuildMergeSourceText(IEnumerable<string> packets)
        {
            return
                "Đây là các bản tóm tắt từng phần của cùng một tài liệu dài. " +
                "Hãy hợp nhất chúng thành một bản tóm tắt thống nhất, đủ ý, không lặp, giữ mạch nội dung rõ ràng. " +
                "Ưu tiên giữ lại các luận điểm, quy trình, khái niệm và dữ kiện quan trọng nhất.\n\n" +
                string.Join("\n\n", packets);
        }

        private static string FormatSummaryPacket(AiSummaryResult partial, int index)
        {
            var points = partial.KeyPoints
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => $"- {x.Trim()}")
                .ToList();

            var builder = new StringBuilder();
            builder.Append("Phần ").Append(index).AppendLine(":");
            builder.AppendLine("Tóm tắt:");
            builder.AppendLine(partial.Summary.Trim());

            if (points.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Ý chính:");
                foreach (var point in points)
                {
                    builder.AppendLine(point);
                }
            }

            return builder.ToString().Trim();
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

        private async Task<SavedSummaryRecord> SaveSummaryRecordAsync(
            string generatedFileName,
            string inputType,
            string originalFileName,
            string? sourceUrl,
            string extractedText,
            string summary,
            List<string> keyPoints,
            double processingTimeSeconds,
            int? userId,
            bool isGuest,
            ContentSafetyReview? safetyReview,
            CancellationToken cancellationToken)
        {
            var sourceType = ResolveSourceTypeForPersistence(inputType, sourceUrl);
            var createdAt = DateTime.UtcNow;
            safetyReview ??= await _contentSafetyService.AnalyzeAsync(
                extractedText,
                summary,
                keyPoints,
                generatedFileName,
                sourceUrl,
                cancellationToken);

            var content = new Web_Project.Models.Content
            {
                UserId = userId,
                IsGuest = isGuest,
                FileName = generatedFileName,
                FileType = ResolveFileTypeForPersistence(inputType, originalFileName),
                FilePath = string.IsNullOrWhiteSpace(sourceUrl) ? originalFileName : sourceUrl,
                SourceType = sourceType,
                SourceUrl = sourceType == "FileUpload" ? null : sourceUrl,
                FetchStatus = sourceType == "FileUpload" ? null : "Completed",
                FetchError = null,
                ExtractedText = TrimToMax(extractedText, 500_000),
                AI_DetectedSubject = safetyReview.IsPolicyViolation ? "Chờ kiểm duyệt chính sách" : BuildDetectedSubject(summary),
                AI_DetectedGrade = string.Empty,
                CreatedAt = createdAt,
                AIProcess = HasPersistableAiProcess(summary, keyPoints)
                    ? new AIProcess
                    {
                        Summary = summary,
                        KeyPoints = SerializeKeyPoints(keyPoints),
                        ProcessingTime = processingTimeSeconds,
                        CreatedAt = createdAt
                    }
                    : null
            };

            if (safetyReview.RequiresAdminReview)
            {
                content.ContentModeration = new ContentModeration
                {
                    Status = "Pending",
                    Reason = TrimToMax(safetyReview.ModerationReason, 1000),
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt,
                };
            }

            _dbContext.Contents.Add(content);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (safetyReview.RequiresAdminReview &&
                userId.HasValue &&
                !isGuest &&
                _systemNotificationService is not null)
            {
                try
                {
                    await _systemNotificationService.NotifyModerationDecisionAsync(
                        adminUserId: 0,
                        content,
                        status: "Pending",
                        reason: safetyReview.ModerationReason,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch pending moderation notification for ContentId={ContentId}", content.ContentId);
                }
            }

            return new SavedSummaryRecord(content.ContentId, safetyReview);
        }

        public async Task<bool> GenerateApprovedContentSummaryAsync(
            int contentId,
            CancellationToken cancellationToken)
        {
            var content = await _dbContext.Contents
                .Include(x => x.AIProcess)
                .Include(x => x.ContentModeration)
                .FirstOrDefaultAsync(x => x.ContentId == contentId, cancellationToken);

            if (content is null ||
                content.AIProcess is not null ||
                content.ContentModeration is null ||
                !string.Equals(content.ContentModeration.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(content.ExtractedText))
            {
                return false;
            }

            var inputType = ResolveInputTypeForStoredContent(content);
            var startedAt = Stopwatch.StartNew();
            var summary = await GenerateSummaryPayloadAsync(content.ExtractedText, inputType, cancellationToken);
            startedAt.Stop();

            content.AI_DetectedSubject = BuildDetectedSubject(summary.Summary);
            content.AI_DetectedGrade = string.Empty;
            content.AIProcess = new AIProcess
            {
                ContentId = content.ContentId,
                Summary = summary.Summary,
                KeyPoints = SerializeKeyPoints(summary.KeyPoints),
                ProcessingTime = startedAt.Elapsed.TotalSeconds,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.SaveChangesAsync(cancellationToken);
            await PersistAiLogAsync("Summary.ApprovedDeferred", content.UserId, content.IsGuest, startedAt.Elapsed.TotalSeconds, isError: false, cancellationToken);
            return true;
        }

        private async Task PersistAiLogAsync(
            string actionType,
            int? userId,
            bool isGuest,
            double processingTimeSeconds,
            bool isError,
            CancellationToken cancellationToken)
        {
            _dbContext.AISystemLogs.Add(new AISystemLog
            {
                ActionType = TrimToMax(actionType, 128),
                UserId = userId,
                IsGuest = isGuest,
                ProcessingTime = Math.Max(0, processingTimeSeconds),
                IsError = isError,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string BuildSummaryActionType(string inputType, bool usedVisionModel, bool usedTranscription)
        {
            if (usedVisionModel)
            {
                return "Summary.Image";
            }

            if (usedTranscription)
            {
                return "Summary.Video";
            }

            var normalized = (inputType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "pdf" => "Summary.Pdf",
                "docx" => "Summary.Docx",
                "webpage" => "Summary.Webpage",
                "video" => "Summary.Video",
                _ => "Summary.Text"
            };
        }

        private async Task<AiSummaryResult> GenerateSummaryPayloadAsync(
            string extractedText,
            string inputType,
            CancellationToken cancellationToken)
        {
            try
            {
                return await SummarizeTextWithChunkingAsync(
                    text: extractedText,
                    sourceHint: inputType,
                    cancellationToken: cancellationToken);
            }
            catch (InvalidOperationException ex) when (IsAiUnavailable(ex.Message))
            {
                _logger.LogWarning(
                    "AI provider unavailable for summarize text. Falling back to local summary. Reason: {Message}",
                    ex.Message);

                return BuildLocalFallbackSummary(extractedText, inputType);
            }
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

        private static bool HasPersistableAiProcess(string summary, List<string> keyPoints)
        {
            return !string.IsNullOrWhiteSpace(summary) ||
                (keyPoints?.Any(point => !string.IsNullOrWhiteSpace(point)) ?? false);
        }

        private static string BuildMeaningfulFileName(string summary, string inputType, string originalFileName)
        {
            var stem = BuildStemFromSummary(summary);
            if (string.IsNullOrWhiteSpace(stem))
            {
                var normalizedType = BuildStemFromSummary(inputType);
                stem = string.IsNullOrWhiteSpace(normalizedType)
                    ? "Tóm tắt"
                    : $"{normalizedType} tóm tắt";
            }

            const int maxStemLength = 72;
            if (stem.Length > maxStemLength)
            {
                stem = stem[..maxStemLength].Trim();
            }

            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "Tóm tắt";
            }

            var extension = ResolveOutputExtension(inputType, originalFileName);
            return $"{stem}{extension}";
        }

        private static string ResolveStoredFileNameForBlockedContent(string originalFileName, string inputType)
        {
            var normalizedOriginal = (originalFileName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedOriginal))
            {
                return normalizedOriginal;
            }

            return BuildMeaningfulFileName(string.Empty, inputType, normalizedOriginal);
        }

        private static string ResolveInputTypeForStoredContent(Web_Project.Models.Content content)
        {
            var fileType = (content.FileType ?? string.Empty).Trim().ToLowerInvariant();
            if (fileType is "pdf" or "docx" or "image" or "video" or "text")
            {
                return fileType;
            }

            if (string.Equals(content.SourceType, "VideoUrl", StringComparison.OrdinalIgnoreCase))
            {
                return "video";
            }

            if (string.Equals(content.SourceType, "TextUrl", StringComparison.OrdinalIgnoreCase))
            {
                return "text";
            }

            return "text";
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

            var text = raw.Trim();

            // Prefer sentence-like file names and keep Vietnamese diacritics intact.
            var sentenceHead = text.Split(new[] { '.', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? text;

            var invalidChars = Path.GetInvalidFileNameChars();
            var cleanedBuilder = new StringBuilder(sentenceHead.Length);
            foreach (var c in sentenceHead)
            {
                cleanedBuilder.Append(invalidChars.Contains(c) ? ' ' : c);
            }

            text = Regex.Replace(cleanedBuilder.ToString(), @"[^\p{L}\p{N}\s]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            var words = text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(12)
                .ToArray();

            if (words.Length == 0)
            {
                return string.Empty;
            }

            var stem = string.Join(' ', words).Trim();
            for (var i = 0; i < stem.Length; i++)
            {
                if (!char.IsLetter(stem[i]))
                {
                    continue;
                }

                var upperFirstChar = stem[i].ToString().ToUpper(VietnameseCulture);
                return stem[..i] + upperFirstChar + stem[(i + 1)..];
            }

            return stem;
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

        private sealed record SavedSummaryRecord(int ContentId, ContentSafetyReview SafetyReview);
    }
}
