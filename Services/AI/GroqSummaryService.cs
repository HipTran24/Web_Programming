using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Web_Project.Models;

namespace Web_Project.Services.AI
{
    public class GroqSummaryService : IGroqSummaryService
    {
        private const string CacheKeyPrefixTextSummary = "ai:summary:text:";
        private const string CacheKeyPrefixImageSummary = "ai:summary:image:";
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> ConcurrencyLimiters = new();

        private readonly HttpClient _httpClient;
        private readonly GroqSettings _settings;
        private readonly ILogger<GroqSummaryService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _requestTimeout;
        private readonly int _maxModelCandidates;
        private readonly int _maxRetriesPerModel;
        private readonly bool _enableResponseCache;
        private readonly TimeSpan _responseCacheDuration;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly TimeSpan _queueWaitTimeout;

        public GroqSummaryService(
            HttpClient httpClient,
            IOptions<GroqSettings> settings,
            IMemoryCache cache,
            ILogger<GroqSummaryService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _cache = cache;
            _logger = logger;
            _requestTimeout = TimeSpan.FromSeconds(Math.Clamp(_settings.RequestTimeoutSeconds, 5, 120));
            _maxModelCandidates = Math.Clamp(_settings.MaxModelCandidates, 1, 5);
            _maxRetriesPerModel = Math.Clamp(_settings.MaxRetriesPerModel, 0, 3);
            _enableResponseCache = _settings.EnableResponseCache;
            _responseCacheDuration = TimeSpan.FromMinutes(Math.Clamp(_settings.ResponseCacheMinutes, 1, 180));
            var maxConcurrent = Math.Clamp(_settings.MaxConcurrentRequests, 1, 16);
            _concurrencyLimiter = ConcurrencyLimiters.GetOrAdd(maxConcurrent, key => new SemaphoreSlim(key, key));
            _queueWaitTimeout = TimeSpan.FromSeconds(Math.Clamp(_settings.QueueWaitTimeoutSeconds, 1, 60));
        }

        public async Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            EnsureGroqApiKey();

            var normalized = NormalizeInputText(text, _settings.MaxInputCharacters);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung văn bản sau xử lý đang rỗng.");
            }

            var cacheKey = BuildTextSummaryCacheKey(normalized, sourceHint);
            if (TryGetCachedSummary(cacheKey, out var cachedSummary))
            {
                return cachedSummary;
            }

            var normalizedSourceHint = (sourceHint ?? string.Empty).Trim().ToLowerInvariant();
            var prompt = normalizedSourceHint == "video"
                ?
                "Bạn là trợ lý học tập SynapLearn chuyên phân tích video bài giảng. " +
                "Từ dữ liệu video bên dưới, hãy tạo tóm tắt nội dung rõ ràng và dễ học. " +
                "Bắt buộc trả về JSON hợp lệ theo định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"[Kiến thức] ...\",\"[Thông tin] ...\",\"[Thuật ngữ] ...\",\"[Mốc thời gian] ...\"]}. " +
                "Yêu cầu: summary dài 220-380 từ; keyPoints có 8-14 ý; mỗi ý cụ thể, có thông tin học được từ video, " +
                "ưu tiên phân biệt đúng: [Kiến thức] cho khái niệm/nguyên lý/phương pháp; [Thông tin] cho dữ kiện/sự kiện/số liệu/chi tiết mô tả. " +
                "không viết chung chung, không markdown, không code fence.\n\n" +
                $"Nguồn: {sourceHint}\n\nNội dung video đã trích xuất:\n{normalized}"
                :
                "Bạn là trợ lý học tập SynapLearn. " +
                "Hãy tóm tắt nội dung sau thành 1 đoạn văn tiếng Việt CHI TIẾT khoảng 280-450 từ, " +
                "không được dưới 250 từ, giữ đủ ý chính theo trình tự nội dung, rõ ràng, không lan man. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                "Không thêm markdown/code fence.\n\n" +
                $"Nguồn: {sourceHint}\n\nNội dung:\n{normalized}";

            var generated = await GenerateChatCompletionAsync(
                model: _settings.TextModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            var parsed = ParseSummaryContent(generated);
            TrySetCachedSummary(cacheKey, parsed);
            return parsed;
        }

        public async Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken)
        {
            EnsureGroqApiKey();

            if (imageBytes.Length == 0)
            {
                throw new InvalidOperationException("Dữ liệu ảnh rỗng.");
            }

            var cacheKey = BuildImageSummaryCacheKey(imageBytes, mimeType, fileName);
            if (TryGetCachedSummary(cacheKey, out var cachedSummary))
            {
                return cachedSummary;
            }

            var prompt =
                "Đọc nội dung trong ảnh này (văn bản, biểu đồ, ghi chú...) và tóm tắt thành 1 đoạn tiếng Việt CHI TIẾT khoảng 220-380 từ. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                $"Tên file: {fileName}. Không dùng markdown.";

            var generated = await GenerateChatCompletionAsync(
                model: _settings.VisionModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: imageBytes,
                imageMimeType: mimeType,
                cancellationToken: cancellationToken);

            var parsed = ParseSummaryContent(generated);
            TrySetCachedSummary(cacheKey, parsed);
            return parsed;
        }

        public async Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            EnsureGroqApiKey();

            if (!File.Exists(audioFilePath))
            {
                throw new InvalidOperationException("Không tìm thấy file audio để phiên âm.");
            }

            await using var audioStream = File.OpenRead(audioFilePath);
            if (audioStream.Length == 0)
            {
                throw new InvalidOperationException("File audio rỗng.");
            }

            if (!await WaitForSlotAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Hệ thống AI đang bận xử lý nhiều yêu cầu. Vui lòng thử lại sau vài giây.");
            }

            try
            {
                string? lastError = null;

                foreach (var candidate in ResolveModelCandidates(_settings.AudioModel))
                {
                    for (var attempt = 0; attempt <= _maxRetriesPerModel; attempt++)
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, BuildAudioTranscriptionEndpoint());
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.GroqApiKey);

                        audioStream.Position = 0;
                        var multipart = new MultipartFormDataContent();
                        multipart.Add(new StringContent(candidate), "model");
                        multipart.Add(new StringContent("text"), "response_format");
                        var audioContent = new StreamContent(audioStream);
                        audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                        multipart.Add(audioContent, "file", Path.GetFileName(audioFilePath));
                        request.Content = multipart;

                        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptCts.CancelAfter(_requestTimeout);

                        HttpResponseMessage response;
                        string responseBody;
                        try
                        {
                            response = await _httpClient.SendAsync(request, attemptCts.Token);
                            responseBody = await response.Content.ReadAsStringAsync(attemptCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            lastError = $"Groq timeout sau {(int)_requestTimeout.TotalSeconds}s với model '{candidate}'.";
                            continue;
                        }

                        using (response)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                var transcript = responseBody.Trim();
                                if (!string.IsNullOrWhiteSpace(transcript))
                                {
                                    return transcript;
                                }

                                throw new InvalidOperationException("Groq không trả transcript từ audio.");
                            }

                            var errorMessage = BuildProviderErrorMessage(response.StatusCode, responseBody, candidate);
                            lastError = errorMessage;

                            if (IsQuotaExceeded(response.StatusCode, responseBody))
                            {
                                throw new InvalidOperationException(errorMessage);
                            }

                            if (attempt < _maxRetriesPerModel && ShouldRetrySameModel(response.StatusCode, responseBody))
                            {
                                var delay = ResolveRetryDelay(response, responseBody);
                                await Task.Delay(delay, cancellationToken);
                                continue;
                            }

                            if (ShouldTryNextModel(response.StatusCode, responseBody))
                            {
                                break;
                            }

                            throw new InvalidOperationException(errorMessage);
                        }
                    }
                }

                throw new InvalidOperationException(
                    lastError ?? "Không thể phiên âm audio với model AI hiện tại.");
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        public async Task<AiQuizResult> GenerateQuizAsync(
            string sourceText,
            int totalQuestions,
            string difficulty,
            string quizType,
            CancellationToken cancellationToken)
        {
            EnsureGroqApiKey();

            var normalized = NormalizeInputText(
                sourceText,
                _settings.MaxQuizInputCharacters > 0
                    ? _settings.MaxQuizInputCharacters
                    : _settings.MaxInputCharacters);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung nguồn để sinh quiz đang rỗng.");
            }

            var boundedQuestionCount = Math.Clamp(totalQuestions, 5, 30);
            var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "medium" : difficulty.Trim().ToLowerInvariant();
            var normalizedQuizType = string.IsNullOrWhiteSpace(quizType) ? "multiple-choice" : quizType.Trim().ToLowerInvariant();

            var prompt =
                "Bạn là trợ lý tạo đề cho hệ thống SynapLearn. " +
                $"Hãy tạo {boundedQuestionCount} câu hỏi dạng trắc nghiệm 4 lựa chọn (A/B/C/D) bằng tiếng Việt dựa trên nội dung dưới đây. " +
                $"Độ khó yêu cầu: {normalizedDifficulty}. Loại bài: {normalizedQuizType}. " +
                "Mỗi câu phải có 1 đáp án đúng duy nhất. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"questions\":[{\"questionText\":\"...\",\"optionA\":\"...\",\"optionB\":\"...\",\"optionC\":\"...\",\"optionD\":\"...\",\"correctAnswer\":\"A\",\"explanation\":\"...\"}]}. " +
                "Không thêm markdown, không code fence, không thêm text ngoài JSON.\n\n" +
                $"Nội dung nguồn:\n{normalized}";

            var generated = await GenerateChatCompletionAsync(
                model: _settings.TextModel,
                prompt: prompt,
                temperature: 0.3,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            return ParseQuizContent(generated, boundedQuestionCount);
        }

        private async Task<string> GenerateChatCompletionAsync(
            string model,
            string prompt,
            double temperature,
            bool requireJsonObject,
            byte[]? imageBytes,
            string? imageMimeType,
            CancellationToken cancellationToken)
        {
            if (!await WaitForSlotAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Hệ thống AI đang bận xử lý nhiều yêu cầu. Vui lòng thử lại sau vài giây.");
            }

            try
            {
                string? lastError = null;

                foreach (var candidate in ResolveModelCandidates(model))
                {
                    for (var attempt = 0; attempt <= _maxRetriesPerModel; attempt++)
                    {
                        var payload = BuildChatPayload(candidate, prompt, temperature, requireJsonObject, imageBytes, imageMimeType);
                        var body = JsonSerializer.Serialize(payload);

                        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionEndpoint());
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.GroqApiKey);
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptCts.CancelAfter(_requestTimeout);

                        HttpResponseMessage response;
                        string responseBody;
                        try
                        {
                            response = await _httpClient.SendAsync(request, attemptCts.Token);
                            responseBody = await response.Content.ReadAsStringAsync(attemptCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            lastError = $"Groq timeout sau {(int)_requestTimeout.TotalSeconds}s với model '{candidate}'.";
                            _logger.LogWarning("Groq timed out for model {Model} after {TimeoutSeconds}s", candidate, (int)_requestTimeout.TotalSeconds);
                            continue;
                        }

                        using (response)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                try
                                {
                                    return ExtractGeneratedText(responseBody);
                                }
                                catch (InvalidOperationException ex)
                                {
                                    _logger.LogWarning(
                                        "Groq parse warning for model {Model}: {Message}. Response: {Response}",
                                        candidate,
                                        ex.Message,
                                        Trim(responseBody, 350));

                                    lastError = $"Groq phản hồi không có text hợp lệ với model '{candidate}': {ex.Message}";
                                    break;
                                }
                            }

                            var isQuotaExceeded = IsQuotaExceeded(response.StatusCode, responseBody);
                            var errorMessage = BuildProviderErrorMessage(response.StatusCode, responseBody, candidate);
                            _logger.LogError("Groq request failed for model {Model}: {Error}", candidate, errorMessage);
                            lastError = errorMessage;

                            if (isQuotaExceeded)
                            {
                                throw new InvalidOperationException(errorMessage);
                            }

                            if (attempt < _maxRetriesPerModel && ShouldRetrySameModel(response.StatusCode, responseBody))
                            {
                                var delay = ResolveRetryDelay(response, responseBody);
                                await Task.Delay(delay, cancellationToken);
                                continue;
                            }

                            if (ShouldTryNextModel(response.StatusCode, responseBody))
                            {
                                _logger.LogWarning("Fallback to next Groq model after failure on {Model}", candidate);
                                break;
                            }

                            throw new InvalidOperationException(errorMessage);
                        }
                    }
                }

                throw new InvalidOperationException(
                    lastError ?? "Không tìm thấy model Groq tương thích để xử lý nội dung.");
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        private static object BuildChatPayload(
            string model,
            string prompt,
            double temperature,
            bool requireJsonObject,
            byte[]? imageBytes,
            string? imageMimeType)
        {
            object[] messages;
            if (imageBytes is { Length: > 0 })
            {
                var mime = string.IsNullOrWhiteSpace(imageMimeType) ? "image/png" : imageMimeType;
                var imageDataUrl = $"data:{mime};base64,{Convert.ToBase64String(imageBytes)}";
                messages =
                [
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = imageDataUrl } }
                        }
                    }
                ];
            }
            else
            {
                messages =
                [
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                ];
            }

            var options = new Dictionary<string, object?>
            {
                ["model"] = NormalizeModel(model),
                ["temperature"] = temperature,
                ["messages"] = messages
            };

            if (requireJsonObject)
            {
                options["response_format"] = new { type = "json_object" };
            }

            return options;
        }

        private static string ExtractGeneratedText(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Phản hồi AI không có choices.");
            }

            if (choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Phản hồi AI có choices rỗng.");
            }

            foreach (var choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty("message", out var message))
                {
                    continue;
                }

                if (!message.TryGetProperty("content", out var content))
                {
                    continue;
                }

                if (content.ValueKind == JsonValueKind.String)
                {
                    var text = content.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }

                if (content.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var part in content.EnumerateArray())
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

                    var text = sb.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            throw new InvalidOperationException("AI trả về choices nhưng không có phần text.");
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

        private static AiQuizResult ParseQuizContent(string rawContent, int requestedQuestions)
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

            using var doc = JsonDocument.Parse(normalized);
            if (!doc.RootElement.TryGetProperty("questions", out var questionsElement) ||
                questionsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("AI không trả về danh sách câu hỏi hợp lệ.");
            }

            var questions = new List<AiQuizQuestion>();
            foreach (var item in questionsElement.EnumerateArray())
            {
                var questionText = item.TryGetProperty("questionText", out var questionTextElement)
                    ? (questionTextElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var optionA = item.TryGetProperty("optionA", out var optionAElement)
                    ? (optionAElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var optionB = item.TryGetProperty("optionB", out var optionBElement)
                    ? (optionBElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var optionC = item.TryGetProperty("optionC", out var optionCElement)
                    ? (optionCElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var optionD = item.TryGetProperty("optionD", out var optionDElement)
                    ? (optionDElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                var correctAnswer = item.TryGetProperty("correctAnswer", out var correctAnswerElement)
                    ? (correctAnswerElement.GetString() ?? string.Empty).Trim().ToUpperInvariant()
                    : string.Empty;

                var explanation = item.TryGetProperty("explanation", out var explanationElement)
                    ? (explanationElement.GetString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(questionText) ||
                    string.IsNullOrWhiteSpace(optionA) ||
                    string.IsNullOrWhiteSpace(optionB) ||
                    string.IsNullOrWhiteSpace(optionC) ||
                    string.IsNullOrWhiteSpace(optionD))
                {
                    continue;
                }

                if (correctAnswer is not ("A" or "B" or "C" or "D"))
                {
                    correctAnswer = "A";
                }

                questions.Add(new AiQuizQuestion
                {
                    QuestionText = questionText,
                    OptionA = optionA,
                    OptionB = optionB,
                    OptionC = optionC,
                    OptionD = optionD,
                    CorrectAnswer = correctAnswer,
                    Explanation = explanation
                });
            }

            if (questions.Count == 0)
            {
                throw new InvalidOperationException("AI không sinh được câu hỏi hợp lệ.");
            }

            if (questions.Count > requestedQuestions)
            {
                questions = questions.Take(requestedQuestions).ToList();
            }

            return new AiQuizResult
            {
                Questions = questions
            };
        }

        private static string NormalizeInputText(string rawText, int maxInputChars)
        {
            var text = (rawText ?? string.Empty).Trim();
            var safeMax = Math.Clamp(maxInputChars, 2000, 100_000);
            if (text.Length <= safeMax)
            {
                return text;
            }

            var headLength = safeMax * 3 / 4;
            var tailLength = safeMax - headLength;

            var head = text[..headLength];
            var tail = text[^tailLength..];
            return $"{head}\n\n...[nội dung đã được rút gọn để vừa ngữ cảnh AI]...\n\n{tail}";
        }

        private void EnsureGroqApiKey()
        {
                if (string.IsNullOrWhiteSpace(_settings.GroqApiKey))
            {
                throw new InvalidOperationException(
                    "Thiếu Groq:GroqApiKey. Vui lòng cấu hình key Groq trước khi gọi endpoint tóm tắt.");
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

            return candidates.Take(_maxModelCandidates);
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
            return (rawModel ?? string.Empty).Trim();
        }

        private string BuildChatCompletionEndpoint()
        {
            var baseUrl = NormalizeBaseUrl(_settings.BaseUrl);
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/chat/completions";
            }

            return $"{baseUrl}/v1/chat/completions";
        }

        private string BuildAudioTranscriptionEndpoint()
        {
            var baseUrl = NormalizeBaseUrl(_settings.BaseUrl);
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/audio/transcriptions";
            }

            return $"{baseUrl}/v1/audio/transcriptions";
        }

        private static string NormalizeBaseUrl(string rawBaseUrl)
        {
            var value = (rawBaseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "https://api.groq.com/openai";
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return "https://api.groq.com/openai";
            }

            var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "https://api.groq.com/openai" : normalized;
        }

        private async Task<bool> WaitForSlotAsync(CancellationToken cancellationToken)
        {
            using var queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            queueCts.CancelAfter(_queueWaitTimeout);

            try
            {
                await _concurrencyLimiter.WaitAsync(queueCts.Token);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        private bool TryGetCachedSummary(string cacheKey, out AiSummaryResult result)
        {
            result = new AiSummaryResult();
            if (!_enableResponseCache)
            {
                return false;
            }

            if (!_cache.TryGetValue(cacheKey, out AiSummaryResult? cached) || cached is null)
            {
                return false;
            }

            result = CloneSummaryResult(cached);
            return true;
        }

        private void TrySetCachedSummary(string cacheKey, AiSummaryResult result)
        {
            if (!_enableResponseCache)
            {
                return;
            }

            _cache.Set(cacheKey, CloneSummaryResult(result), _responseCacheDuration);
        }

        private static AiSummaryResult CloneSummaryResult(AiSummaryResult source)
        {
            return new AiSummaryResult
            {
                Summary = source.Summary,
                KeyPoints = [.. source.KeyPoints]
            };
        }

        private static string BuildTextSummaryCacheKey(string normalizedText, string sourceHint)
        {
            var hash = ComputeSha256Hex($"{sourceHint}\n{normalizedText}");
            return $"{CacheKeyPrefixTextSummary}{hash}";
        }

        private static string BuildImageSummaryCacheKey(byte[] imageBytes, string mimeType, string fileName)
        {
            var bytesHash = Convert.ToHexString(SHA256.HashData(imageBytes));
            var metaHash = ComputeSha256Hex($"{mimeType}|{fileName}");
            return $"{CacheKeyPrefixImageSummary}{metaHash}:{bytesHash}";
        }

        private static string ComputeSha256Hex(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static bool IsQuotaExceeded(HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode != HttpStatusCode.TooManyRequests)
            {
                return false;
            }

            var message = (responseBody ?? string.Empty).ToLowerInvariant();
            return message.Contains("resource_exhausted") ||
                   message.Contains("quota") ||
                   message.Contains("rate limit") ||
                   message.Contains("too many requests") ||
                   message.Contains("tokens per minute");
        }

        private static bool ShouldRetrySameModel(HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                return !IsQuotaExceeded(statusCode, responseBody);
            }

            if (statusCode == HttpStatusCode.ServiceUnavailable ||
                statusCode == HttpStatusCode.GatewayTimeout ||
                statusCode == HttpStatusCode.BadGateway ||
                statusCode == HttpStatusCode.InternalServerError)
            {
                return true;
            }

            var message = (responseBody ?? string.Empty).ToLowerInvariant();
            return message.Contains("high demand") ||
                   message.Contains("temporarily unavailable") ||
                   message.Contains("try again later") ||
                   message.Contains("overloaded");
        }

        private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, string responseBody)
        {
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero)
            {
                return ClampRetryDelay(delta);
            }

            if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
            {
                var diff = date - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero)
                {
                    return ClampRetryDelay(diff);
                }
            }

            var seconds = ExtractRetryHintSecondsNumeric(responseBody);
            if (seconds > 0)
            {
                return ClampRetryDelay(TimeSpan.FromSeconds(seconds));
            }

            return TimeSpan.FromSeconds(2);
        }

        private static TimeSpan ClampRetryDelay(TimeSpan delay)
        {
            if (delay < TimeSpan.FromMilliseconds(300))
            {
                return TimeSpan.FromMilliseconds(300);
            }

            if (delay > TimeSpan.FromSeconds(12))
            {
                return TimeSpan.FromSeconds(12);
            }

            return delay;
        }

        private static double ExtractRetryHintSecondsNumeric(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return 0;
            }

            var match = Regex.Match(message, @"retry\s+in\s+([0-9]+(?:\.[0-9]+)?)s", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : 0;
        }

        private static string BuildProviderErrorMessage(HttpStatusCode statusCode, string responseBody, string model)
        {
            var providerCode = (int)statusCode;
            var providerType = string.Empty;
            var providerMessage = string.Empty;

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        if (errorElement.TryGetProperty("code", out var codeElement) &&
                            codeElement.ValueKind == JsonValueKind.Number)
                        {
                            providerCode = codeElement.GetInt32();
                        }

                        if (errorElement.TryGetProperty("type", out var typeElement) &&
                            typeElement.ValueKind == JsonValueKind.String)
                        {
                            providerType = typeElement.GetString() ?? string.Empty;
                        }

                        if (errorElement.TryGetProperty("message", out var messageElement) &&
                            messageElement.ValueKind == JsonValueKind.String)
                        {
                            providerMessage = messageElement.GetString() ?? string.Empty;
                        }
                    }
                }
                catch
                {
                    // Keep fallback values.
                }
            }

            var normalized = $"{providerType} {providerMessage}".ToLowerInvariant();
            var isQuotaOrRateLimit = providerCode == 429 ||
                                     normalized.Contains("resource_exhausted") ||
                                     normalized.Contains("quota") ||
                                     normalized.Contains("rate limit") ||
                                     normalized.Contains("too many requests");

            if (isQuotaOrRateLimit)
            {
                var retryHint = ExtractRetryHintSeconds(providerMessage);
                return string.IsNullOrWhiteSpace(retryHint)
                    ? "Groq Cloud đang tạm quá tải hoặc vượt giới hạn sử dụng. Vui lòng thử lại sau ít phút."
                    : $"Groq Cloud đang tạm quá tải hoặc vượt giới hạn sử dụng. Vui lòng thử lại sau khoảng {retryHint}.";
            }

            if (providerCode == 401 || providerCode == 403 ||
                normalized.Contains("api key") ||
                normalized.Contains("permission") ||
                normalized.Contains("unauthorized"))
            {
                return "Groq API key không hợp lệ hoặc chưa đủ quyền. Vui lòng kiểm tra cấu hình.";
            }

            if (providerCode == 404 || normalized.Contains("model") && normalized.Contains("not"))
            {
                return $"Model AI '{model}' hiện không khả dụng trên Groq. Hệ thống sẽ thử model dự phòng.";
            }

            if (providerCode >= 500 ||
                normalized.Contains("temporarily unavailable") ||
                normalized.Contains("high demand") ||
                normalized.Contains("overloaded"))
            {
                return "Dịch vụ Groq đang bận. Vui lòng thử lại sau ít phút.";
            }

            return $"Không thể xử lý yêu cầu AI lúc này (mã {providerCode}). Vui lòng thử lại.";
        }

        private static string ExtractRetryHintSeconds(string message)
        {
            var seconds = ExtractRetryHintSecondsNumeric(message);
            if (seconds <= 0)
            {
                return string.Empty;
            }

            var rounded = Math.Clamp((int)Math.Ceiling(seconds), 1, 3600);
            return rounded < 60
                ? $"{rounded} giây"
                : $"{(int)Math.Ceiling(rounded / 60d)} phút";
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
                if (statusCode == HttpStatusCode.TooManyRequests && IsQuotaExceeded(statusCode, responseBody))
                {
                    return false;
                }

                return true;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            var message = responseBody.ToLowerInvariant();
            return message.Contains("model") && message.Contains("not") ||
                   message.Contains("high demand") ||
                   message.Contains("try again later") ||
                   message.Contains("temporarily unavailable") ||
                   message.Contains("overloaded");
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
