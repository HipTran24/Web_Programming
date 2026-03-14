using System.Text;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.Options;
using Web_Project.Models;

namespace Web_Project.Services.AI
{
    public class GeminiSummaryService : IGeminiSummaryService
    {
        private const int MaxInlineAudioBytes = 20 * 1024 * 1024;

        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _settings;
        private readonly ILogger<GeminiSummaryService> _logger;

        public GeminiSummaryService(
            HttpClient httpClient,
            IOptions<GeminiSettings> settings,
            ILogger<GeminiSummaryService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            EnsureApiKey();

            var normalized = NormalizeInputText(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung văn bản sau xử lý đang rỗng.");
            }

            var prompt =
                "Bạn là trợ lý học tập AI Study. " +
                "Hãy tóm tắt nội dung sau thành 1 đoạn văn tiếng Việt CHI TIẾT khoảng 280-450 từ, " +
                "không được dưới 250 từ, giữ đủ ý chính theo trình tự nội dung, rõ ràng, không lan man. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                "Không thêm markdown/code fence.\n\n" +
                $"Nguồn: {sourceHint}\n\nNội dung:\n{normalized}";

            var generated = await GenerateContentAsync(
                model: _settings.TextModel,
                parts: [new { text = prompt }],
                temperature: 0.2,
                responseMimeType: null,
                cancellationToken: cancellationToken);

            return ParseSummaryContent(generated);
        }

        public async Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken)
        {
            EnsureApiKey();

            if (imageBytes.Length == 0)
            {
                throw new InvalidOperationException("Dữ liệu ảnh rỗng.");
            }

            var prompt =
                "Đọc nội dung trong ảnh này (văn bản, biểu đồ, ghi chú...) và tóm tắt thành 1 đoạn tiếng Việt CHI TIẾT khoảng 220-380 từ. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                $"Tên file: {fileName}. Không dùng markdown.";

            var generated = await GenerateContentAsync(
                model: _settings.VisionModel,
                parts:
                [
                    new { text = prompt },
                    new
                    {
                        inlineData = new
                        {
                            mimeType,
                            data = Convert.ToBase64String(imageBytes)
                        }
                    }
                ],
                temperature: 0.2,
                responseMimeType: null,
                cancellationToken: cancellationToken);

            return ParseSummaryContent(generated);
        }

        public async Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            EnsureApiKey();

            if (!File.Exists(audioFilePath))
            {
                throw new InvalidOperationException("Không tìm thấy file audio để phiên âm.");
            }

            var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            if (audioBytes.Length == 0)
            {
                throw new InvalidOperationException("File audio rỗng.");
            }

            if (audioBytes.Length > MaxInlineAudioBytes)
            {
                throw new InvalidOperationException(
                    "Audio sau khi tách từ video quá lớn cho Gemini inline input. Hãy dùng video ngắn hơn.");
            }

            var generated = await GenerateContentAsync(
                model: _settings.AudioModel,
                parts:
                [
                    new
                    {
                        text = "Hãy phiên âm chính xác audio sau thành văn bản tiếng Việt thuần, không thêm chú thích."
                    },
                    new
                    {
                        inlineData = new
                        {
                            mimeType = "audio/mpeg",
                            data = Convert.ToBase64String(audioBytes)
                        }
                    }
                ],
                temperature: 0.0,
                responseMimeType: null,
                cancellationToken: cancellationToken);

            var transcript = generated.Trim();
            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new InvalidOperationException("Gemini không trả transcript từ audio.");
            }

            return transcript;
        }

        private async Task<string> GenerateContentAsync(
            string model,
            object[] parts,
            double temperature,
            string? responseMimeType,
            CancellationToken cancellationToken)
        {
            var generationConfig = new Dictionary<string, object>
            {
                ["temperature"] = temperature
            };

            if (!string.IsNullOrWhiteSpace(responseMimeType))
            {
                generationConfig["responseMimeType"] = responseMimeType;
            }

            var payload = new
            {
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts
                    }
                },
                generationConfig
            };

            var body = JsonSerializer.Serialize(payload);
            string? lastError = null;

            foreach (var candidate in ResolveModelCandidates(model))
            {
                var endpoint = BuildEndpoint(candidate);
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return ExtractGeneratedText(responseBody);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(
                            "Gemini parse warning for model {Model}: {Message}. Response: {Response}",
                            candidate,
                            ex.Message,
                            Trim(responseBody, 350));

                        lastError = $"Gemini phản hồi không có text hợp lệ với model '{candidate}': {ex.Message}";
                        continue;
                    }
                }

                var errorMessage = BuildGeminiErrorMessage(response.StatusCode, responseBody, candidate);
                _logger.LogError("Gemini request failed for model {Model}: {Error}", candidate, errorMessage);
                lastError = errorMessage;

                if (ShouldTryNextModel(response.StatusCode, responseBody))
                {
                    _logger.LogWarning("Fallback to next Gemini model after failure on {Model}", candidate);
                    continue;
                }

                throw new InvalidOperationException(errorMessage);
            }

            throw new InvalidOperationException(
                lastError ?? "Không tìm thấy model Gemini tương thích để xử lý nội dung.");
        }

        private static string ExtractGeneratedText(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array)
            {
                var blocked = ReadPromptBlockReason(root);
                if (!string.IsNullOrWhiteSpace(blocked))
                {
                    throw new InvalidOperationException($"Gemini chặn nội dung đầu vào: {blocked}.");
                }

                throw new InvalidOperationException("Phản hồi Gemini không có candidates.");
            }

            if (candidates.GetArrayLength() == 0)
            {
                var blocked = ReadPromptBlockReason(root);
                if (!string.IsNullOrWhiteSpace(blocked))
                {
                    throw new InvalidOperationException($"Gemini chặn nội dung đầu vào: {blocked}.");
                }

                throw new InvalidOperationException("Phản hồi Gemini có candidates rỗng.");
            }

            var sb = new StringBuilder();
            string? lastFinishReason = null;
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.TryGetProperty("finishReason", out var finishReasonElement))
                {
                    lastFinishReason = finishReasonElement.GetString();
                }

                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElement))
                    {
                        var value = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            sb.AppendLine(value);
                        }
                    }
                }
            }

            var text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var promptBlockReason = ReadPromptBlockReason(root);
            if (!string.IsNullOrWhiteSpace(promptBlockReason))
            {
                throw new InvalidOperationException($"Gemini chặn nội dung đầu vào: {promptBlockReason}.");
            }

            if (!string.IsNullOrWhiteSpace(lastFinishReason))
            {
                throw new InvalidOperationException($"Gemini không trả text. finishReason={lastFinishReason}.");
            }

            throw new InvalidOperationException("Gemini trả về candidates nhưng không có phần text.");
        }

        private static string? ReadPromptBlockReason(JsonElement root)
        {
            if (!root.TryGetProperty("promptFeedback", out var promptFeedback))
            {
                return null;
            }

            var reasons = new List<string>();
            if (promptFeedback.TryGetProperty("blockReason", out var blockReason) &&
                blockReason.ValueKind == JsonValueKind.String)
            {
                var value = blockReason.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    reasons.Add(value.Trim());
                }
            }

            if (promptFeedback.TryGetProperty("blockReasonMessage", out var blockReasonMessage) &&
                blockReasonMessage.ValueKind == JsonValueKind.String)
            {
                var value = blockReasonMessage.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    reasons.Add(value.Trim());
                }
            }

            if (reasons.Count == 0)
            {
                return null;
            }

            return string.Join(" - ", reasons);
        }

        private static AiSummaryResult ParseSummaryContent(string rawContent)
        {
            var normalized = rawContent.Trim();
            if (normalized.StartsWith("```", StringComparison.Ordinal))
            {
                normalized = normalized.Trim('`').Trim();
                if (normalized.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[4..].Trim();
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(normalized);
                var summary = doc.RootElement.TryGetProperty("summary", out var summaryElement)
                    ? (summaryElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var points = new List<string>();
                if (doc.RootElement.TryGetProperty("keyPoints", out var keyPointsElement) &&
                    keyPointsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in keyPointsElement.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            points.Add(value.Trim());
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(summary))
                {
                    throw new InvalidOperationException("Summary trống.");
                }

                return new AiSummaryResult
                {
                    Summary = summary,
                    KeyPoints = points
                };
            }
            catch
            {
                return new AiSummaryResult
                {
                    Summary = rawContent.Trim(),
                    KeyPoints = []
                };
            }
        }

        private string NormalizeInputText(string rawText)
        {
            var text = (rawText ?? string.Empty).Trim();
            if (text.Length <= _settings.MaxInputCharacters)
            {
                return text;
            }

            var headLength = _settings.MaxInputCharacters * 3 / 4;
            var tailLength = _settings.MaxInputCharacters - headLength;

            var head = text[..headLength];
            var tail = text[^tailLength..];
            return $"{head}\n\n...[nội dung đã được rút gọn để vừa ngữ cảnh AI]...\n\n{tail}";
        }

        private void EnsureApiKey()
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Thiếu Gemini:ApiKey. Vui lòng cấu hình API key trước khi gọi endpoint tóm tắt.");
            }
        }

        private IEnumerable<string> ResolveModelCandidates(string primaryModel)
        {
            var candidates = new List<string>();

            AddCandidate(candidates, primaryModel);

            foreach (var fallback in _settings.FallbackModels)
            {
                AddCandidate(candidates, fallback);
            }

            AddCandidate(candidates, "gemini-2.5-flash");
            AddCandidate(candidates, "gemini-2.0-flash");
            AddCandidate(candidates, "gemini-flash-latest");

            return candidates;
        }

        private static void AddCandidate(List<string> candidates, string? rawModel)
        {
            var normalized = NormalizeModel(rawModel);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (candidates.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            candidates.Add(normalized);
        }

        private static string NormalizeModel(string? rawModel)
        {
            var normalizedModel = (rawModel ?? string.Empty).Trim();
            if (normalizedModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedModel = normalizedModel["models/".Length..];
            }

            return normalizedModel;
        }

        private string BuildEndpoint(string model)
        {
            var baseUrl = NormalizeBaseUrl(_settings.BaseUrl);
            var normalizedModel = NormalizeModel(model);

            if (string.IsNullOrWhiteSpace(normalizedModel))
            {
                normalizedModel = "gemini-2.0-flash";
            }

            var encodedModel = Uri.EscapeDataString(normalizedModel);
            var encodedKey = Uri.EscapeDataString(_settings.ApiKey);
            return $"{baseUrl}/v1beta/models/{encodedModel}:generateContent?key={encodedKey}";
        }

        private static string NormalizeBaseUrl(string rawBaseUrl)
        {
            var value = (rawBaseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "https://generativelanguage.googleapis.com";
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return "https://generativelanguage.googleapis.com";
            }

            return $"{uri.Scheme}://{uri.Authority}";
        }

        private static string BuildGeminiErrorMessage(HttpStatusCode statusCode, string responseBody, string model)
        {
            var fallback = $"Gemini lỗi ({(int)statusCode}) khi dùng model '{model}'.";
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return fallback;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (!doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    return $"{fallback} Response: {Trim(responseBody, 300)}";
                }

                var code = errorElement.TryGetProperty("code", out var codeElement)
                    ? codeElement.GetInt32()
                    : (int)statusCode;
                var status = errorElement.TryGetProperty("status", out var statusElement)
                    ? (statusElement.GetString() ?? string.Empty)
                    : string.Empty;
                var message = errorElement.TryGetProperty("message", out var messageElement)
                    ? (messageElement.GetString() ?? string.Empty)
                    : string.Empty;

                var reason = string.IsNullOrWhiteSpace(message) ? Trim(responseBody, 300) : message;
                return $"Gemini lỗi ({code} {status}) với model '{model}': {reason}";
            }
            catch
            {
                return $"{fallback} Response: {Trim(responseBody, 300)}";
            }
        }

        private static bool ShouldTryNextModel(HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode == HttpStatusCode.NotFound)
            {
                return true;
            }

            if (statusCode == HttpStatusCode.TooManyRequests ||
                statusCode == HttpStatusCode.ServiceUnavailable ||
                statusCode == HttpStatusCode.GatewayTimeout ||
                statusCode == HttpStatusCode.BadGateway ||
                statusCode == HttpStatusCode.InternalServerError)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            var message = responseBody.ToLowerInvariant();
            return message.Contains("is not found") ||
                   message.Contains("not supported for generatecontent") ||
                   message.Contains("model not found") ||
                   message.Contains("high demand") ||
                   message.Contains("try again later") ||
                   message.Contains("temporarily unavailable");
        }

        private static string Trim(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return $"{value[..maxLength]}...";
        }
    }
}
