using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Web_Project.Models;

namespace Web_Project.Services.AI
{
    public class GroqSummaryService : IGroqSummaryService
    {
        private const string CacheKeyPrefixTextSummary = "ai:summary:text:";
        private const string CacheKeyPrefixImageSummary = "ai:summary:image:";
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> ConcurrencyLimiters = new();

        private readonly HttpClient _httpClient;
        private readonly IAiRuntimeSettingsService _runtimeSettings;
        private readonly ILogger<GroqSummaryService> _logger;
        private readonly IMemoryCache _cache;

        public GroqSummaryService(
            HttpClient httpClient,
            IAiRuntimeSettingsService runtimeSettings,
            IMemoryCache cache,
            ILogger<GroqSummaryService> logger)
        {
            _httpClient = httpClient;
            _runtimeSettings = runtimeSettings;
            _cache = cache;
            _logger = logger;
        }

        public async Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Groq;
            EnsureGroqApiKey(settings);

            var normalized = NormalizeInputText(text, settings.MaxInputCharacters);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung văn bản sau xử lý đang rỗng.");
            }

            var cacheKey = BuildTextSummaryCacheKey(normalized, sourceHint);
            if (TryGetCachedSummary(settings, cacheKey, out var cachedSummary))
            {
                return cachedSummary;
            }

            var normalizedSourceHint = (sourceHint ?? string.Empty).Trim().ToLowerInvariant();
            var prompt = normalizedSourceHint == "video"
                ?
                "Bạn là trợ lý học tập SynapLearn chuyên phân tích video bài giảng. " +
                "Hãy viết tóm tắt tiếng Việt CHI TIẾT, đầy đủ ý nghĩa và có chiều sâu học thuật từ dữ liệu video bên dưới. " +
                "Tóm tắt phải giúp người đọc hiểu được: bối cảnh, luận điểm chính, các ý phụ quan trọng, ví dụ/số liệu nổi bật, kết luận hoặc thông điệp cốt lõi. " +
                "Bắt buộc trả về JSON hợp lệ theo đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"[Kiến thức] ...\",\"[Thông tin] ...\",\"[Thuật ngữ] ...\"]}. " +
                "Yêu cầu chi tiết: " +
                "summary dài 420-650 từ; " +
                "viết thành nhiều câu mạch lạc, theo đúng tiến trình nội dung video; " +
                "giữ nguyên ý quan trọng, không suy diễn; " +
                "nêu rõ mối liên hệ nguyên nhân-kết quả khi có; " +
                "nếu có thuật ngữ thì giải thích ngắn gọn theo ngữ cảnh. " +
                "keyPoints có 10-18 ý, mỗi ý 1 câu rõ nghĩa, không trùng lặp, có giá trị học tập thực tế. " +
                "Ưu tiên gắn nhãn đúng: [Kiến thức] cho khái niệm/nguyên lý/phương pháp; [Thông tin] cho dữ kiện/sự kiện/số liệu; [Thuật ngữ] cho định nghĩa thuật ngữ. " +
                "Không tạo keypoint chỉ để ghi mốc giờ/phút/giây hoặc timeline. " +
                "Không markdown, không code fence, không thêm text ngoài JSON.\n\n" +
                $"Nguồn: {sourceHint}\n\nNội dung video đã trích xuất:\n{normalized}"
                :
                "Bạn là trợ lý học tập SynapLearn. " +
                "Hãy tóm tắt nội dung sau thành bản tóm tắt tiếng Việt CHI TIẾT, đầy đủ ý nghĩa, dễ học và có chiều sâu. " +
                "Bản tóm tắt phải bao quát bối cảnh/chủ đề, các luận điểm chính, ý phụ quan trọng, ví dụ hoặc dữ kiện đáng chú ý, và kết luận cốt lõi. " +
                "Độ dài summary: 420-650 từ, tuyệt đối không dưới 380 từ. " +
                "Trình bày mạch lạc theo đúng trật tự nội dung gốc; không liệt kê rời rạc, không lan man, không mất ý quan trọng. " +
                "Nếu tài liệu có quy trình hoặc lập luận, hãy thể hiện rõ các bước và quan hệ nhân quả. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                "keyPoints cần 10-16 ý, ngắn gọn nhưng đủ nghĩa, mỗi ý phản ánh một thông tin quan trọng không trùng lặp. " +
                "Không thêm markdown/code fence.\n\n" +
                $"Nguồn: {sourceHint}\n\nNội dung:\n{normalized}";

            var generated = await GenerateChatCompletionAsync(
                settings: settings,
                model: settings.TextModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            var parsed = ParseSummaryContent(generated);
            TrySetCachedSummary(settings, cacheKey, parsed);
            return parsed;
        }

        public async Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Groq;
            EnsureGroqApiKey(settings);

            if (imageBytes.Length == 0)
            {
                throw new InvalidOperationException("Dữ liệu ảnh rỗng.");
            }

            var cacheKey = BuildImageSummaryCacheKey(imageBytes, mimeType, fileName);
            if (TryGetCachedSummary(settings, cacheKey, out var cachedSummary))
            {
                return cachedSummary;
            }

            var prompt =
                "Đọc nội dung trong ảnh này (văn bản, biểu đồ, ghi chú...) và tóm tắt thành 1 đoạn tiếng Việt CHI TIẾT khoảng 220-380 từ. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"summary\":\"...\",\"keyPoints\":[\"...\",\"...\"]}. " +
                $"Tên file: {fileName}. Không dùng markdown.";

            var generated = await GenerateChatCompletionAsync(
                settings: settings,
                model: settings.VisionModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: imageBytes,
                imageMimeType: mimeType,
                cancellationToken: cancellationToken);

            var parsed = ParseSummaryContent(generated);
            TrySetCachedSummary(settings, cacheKey, parsed);
            return parsed;
        }

        public async Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Groq;
            EnsureGroqApiKey(settings);

            if (!File.Exists(audioFilePath))
            {
                throw new InvalidOperationException("Không tìm thấy file audio để phiên âm.");
            }

            await using var audioStream = File.OpenRead(audioFilePath);
            if (audioStream.Length == 0)
            {
                throw new InvalidOperationException("File audio rỗng.");
            }

            var limiter = await AcquireSlotAsync(settings, cancellationToken);
            if (limiter is null)
            {
                throw new InvalidOperationException(
                    "Hệ thống AI đang bận xử lý nhiều yêu cầu. Vui lòng thử lại sau vài giây.");
            }

            try
            {
                var requestTimeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120));
                var maxRetriesPerModel = Math.Clamp(settings.MaxRetriesPerModel, 0, 3);
                var maxModelCandidates = Math.Clamp(settings.MaxModelCandidates, 1, 5);
                string? lastError = null;

                foreach (var candidate in ResolveModelCandidates(settings, settings.AudioModel, maxModelCandidates))
                {
                    for (var attempt = 0; attempt <= maxRetriesPerModel; attempt++)
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, BuildAudioTranscriptionEndpoint(settings));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.GroqApiKey);

                        audioStream.Position = 0;
                        var multipart = new MultipartFormDataContent();
                        multipart.Add(new StringContent(candidate), "model");
                        multipart.Add(new StringContent("text"), "response_format");
                        var audioContent = new StreamContent(audioStream);
                        audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                        multipart.Add(audioContent, "file", Path.GetFileName(audioFilePath));
                        request.Content = multipart;

                        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptCts.CancelAfter(requestTimeout);

                        HttpResponseMessage response;
                        string responseBody;
                        try
                        {
                            response = await _httpClient.SendAsync(request, attemptCts.Token);
                            responseBody = await response.Content.ReadAsStringAsync(attemptCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            lastError = $"Groq timeout sau {(int)requestTimeout.TotalSeconds}s với model '{candidate}'.";
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

                            if (attempt < maxRetriesPerModel && ShouldRetrySameModel(response.StatusCode, responseBody))
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
                limiter.Release();
            }
        }

        public async Task<AiQuizResult> GenerateQuizAsync(
            string sourceText,
            int totalQuestions,
            string difficulty,
            string quizType,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Groq;
            EnsureGroqApiKey(settings);

            var normalized = NormalizeInputText(
                sourceText,
                settings.MaxQuizInputCharacters > 0
                    ? settings.MaxQuizInputCharacters
                    : settings.MaxInputCharacters);
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
                "TUYET DOI KHONG tao câu hỏi yêu cầu nhớ mốc thời gian/timestamp trong video (ví dụ: 'ở thời điểm nào', 'tại phút giây nào', 'xuất hiện lúc 00:01:23', 'theo timeline'). " +
                "Chi tao câu hỏi kiểm tra hiểu biết nội dung: khái niệm, bản chất, nguyên lý, quy trình, mục tiêu, so sánh, ứng dụng, kết luận. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"questions\":[{\"questionText\":\"...\",\"optionA\":\"...\",\"optionB\":\"...\",\"optionC\":\"...\",\"optionD\":\"...\",\"correctAnswer\":\"A\",\"explanation\":\"...\"}]}. " +
                "Không thêm markdown, không code fence, không thêm text ngoài JSON.\n\n" +
                $"Nội dung nguồn:\n{normalized}";

            var generated = await GenerateChatCompletionAsync(
                settings: settings,
                model: settings.TextModel,
                prompt: prompt,
                temperature: 0.3,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            return ParseQuizContent(generated, boundedQuestionCount);
        }

        private async Task<string> GenerateChatCompletionAsync(
            GroqSettings settings,
            string model,
            string prompt,
            double temperature,
            bool requireJsonObject,
            byte[]? imageBytes,
            string? imageMimeType,
            CancellationToken cancellationToken)
        {
            var limiter = await AcquireSlotAsync(settings, cancellationToken);
            if (limiter is null)
            {
                throw new InvalidOperationException(
                    "Hệ thống AI đang bận xử lý nhiều yêu cầu. Vui lòng thử lại sau vài giây.");
            }

            try
            {
                var requestTimeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120));
                var maxRetriesPerModel = Math.Clamp(settings.MaxRetriesPerModel, 0, 3);
                var maxModelCandidates = Math.Clamp(settings.MaxModelCandidates, 1, 5);
                string? lastError = null;

                foreach (var candidate in ResolveModelCandidates(settings, model, maxModelCandidates))
                {
                    for (var attempt = 0; attempt <= maxRetriesPerModel; attempt++)
                    {
                        var payload = BuildChatPayload(candidate, prompt, temperature, requireJsonObject, imageBytes, imageMimeType);
                        var body = JsonSerializer.Serialize(payload);

                        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionEndpoint(settings));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.GroqApiKey);
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        attemptCts.CancelAfter(requestTimeout);

                        HttpResponseMessage response;
                        string responseBody;
                        try
                        {
                            response = await _httpClient.SendAsync(request, attemptCts.Token);
                            responseBody = await response.Content.ReadAsStringAsync(attemptCts.Token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            lastError = $"Groq timeout sau {(int)requestTimeout.TotalSeconds}s với model '{candidate}'.";
                            _logger.LogWarning("Groq timed out for model {Model} after {TimeoutSeconds}s", candidate, (int)requestTimeout.TotalSeconds);
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

                            if (attempt < maxRetriesPerModel && ShouldRetrySameModel(response.StatusCode, responseBody))
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
                limiter.Release();
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
            var normalized = NormalizeModelPayload(rawContent);

            try
            {
                using var doc = JsonDocument.Parse(normalized);
                var summary = doc.RootElement.TryGetProperty("summary", out var summaryElement)
                    ? CleanModelArtifacts(summaryElement.GetString())
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
                            var cleaned = CleanModelArtifacts(value);
                            if (!string.IsNullOrWhiteSpace(cleaned))
                            {
                                points.Add(cleaned);
                            }
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
                    Summary = CleanModelArtifacts(rawContent),
                    KeyPoints = []
                };
            }
        }

        private static AiQuizResult ParseQuizContent(string rawContent, int requestedQuestions)
        {
            var normalized = NormalizeModelPayload(rawContent);

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
                    ? CleanModelArtifacts(questionTextElement.GetString())
                    : string.Empty;

                var optionA = item.TryGetProperty("optionA", out var optionAElement)
                    ? CleanModelArtifacts(optionAElement.GetString())
                    : string.Empty;

                var optionB = item.TryGetProperty("optionB", out var optionBElement)
                    ? CleanModelArtifacts(optionBElement.GetString())
                    : string.Empty;

                var optionC = item.TryGetProperty("optionC", out var optionCElement)
                    ? CleanModelArtifacts(optionCElement.GetString())
                    : string.Empty;

                var optionD = item.TryGetProperty("optionD", out var optionDElement)
                    ? CleanModelArtifacts(optionDElement.GetString())
                    : string.Empty;

                var correctAnswer = item.TryGetProperty("correctAnswer", out var correctAnswerElement)
                    ? (correctAnswerElement.GetString() ?? string.Empty).Trim().ToUpperInvariant()
                    : string.Empty;

                var explanation = item.TryGetProperty("explanation", out var explanationElement)
                    ? CleanModelArtifacts(explanationElement.GetString())
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

        private static string NormalizeModelPayload(string rawContent)
        {
            var normalized = (rawContent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            normalized = StripCodeFence(normalized);
            normalized = CleanModelArtifacts(normalized);

            // Some providers return an envelope where the real JSON is nested as a string.
            for (var i = 0; i < 2; i++)
            {
                if (!TryExtractNestedJson(normalized, out var nestedJson))
                {
                    break;
                }

                normalized = CleanModelArtifacts(StripCodeFence(nestedJson));
            }

            return normalized;
        }

        private static bool TryExtractNestedJson(string input, out string nestedJson)
        {
            nestedJson = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(input);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (root.TryGetProperty("summary", out _) || root.TryGetProperty("questions", out _))
                {
                    return false;
                }

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var candidateObject = property.Value.GetRawText().Trim();
                        if (LooksLikeJson(candidateObject))
                        {
                            nestedJson = candidateObject;
                            return true;
                        }

                        continue;
                    }

                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var value = property.Value.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    var candidate = value.Trim();
                    if (LooksLikeJson(candidate))
                    {
                        nestedJson = candidate;
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeJson(string value)
        {
            var text = value.Trim();
            return (text.StartsWith('{') && text.EndsWith('}')) || (text.StartsWith('[') && text.EndsWith(']'));
        }

        private static string StripCodeFence(string value)
        {
            var text = value.Trim();
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            text = text.Trim('`').Trim();
            if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                text = text[4..].Trim();
            }

            return text;
        }

        private static string CleanModelArtifacts(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = Regex.Replace(text, "^\\s*assistant\\s*<\\|[^|]+\\|>\\s*:\\s*", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "^\\s*assistant\\s*:\\s*", string.Empty, RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<\\|[^|]+\\|>", string.Empty, RegexOptions.IgnoreCase);

            return text.Trim();
        }

        private static string NormalizeInputText(string rawText, int maxInputChars)
        {
            var text = (rawText ?? string.Empty).Trim();
            if (maxInputChars <= 0)
            {
                return text;
            }

            var safeMax = Math.Clamp(maxInputChars, 2000, 1_000_000);
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

        private static void EnsureGroqApiKey(GroqSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.GroqApiKey))
            {
                throw new InvalidOperationException(
                    "Thiếu Groq:GroqApiKey. Vui lòng cấu hình key Groq trước khi gọi endpoint tóm tắt.");
            }
        }

        private static IEnumerable<string> ResolveModelCandidates(GroqSettings settings, string primaryModel, int maxModelCandidates)
        {
            var candidates = new List<string>();

            AddCandidate(candidates, primaryModel);

            foreach (var fallback in settings.FallbackModels)
            {
                AddCandidate(candidates, fallback);
            }

            return candidates.Take(maxModelCandidates);
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

        private static string BuildChatCompletionEndpoint(GroqSettings settings)
        {
            var baseUrl = NormalizeBaseUrl(settings.BaseUrl);
            if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/chat/completions";
            }

            return $"{baseUrl}/v1/chat/completions";
        }

        private static string BuildAudioTranscriptionEndpoint(GroqSettings settings)
        {
            var baseUrl = NormalizeBaseUrl(settings.BaseUrl);
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

        private static SemaphoreSlim GetConcurrencyLimiter(GroqSettings settings)
        {
            var maxConcurrent = Math.Clamp(settings.MaxConcurrentRequests, 1, 16);
            return ConcurrencyLimiters.GetOrAdd(maxConcurrent, key => new SemaphoreSlim(key, key));
        }

        private static TimeSpan GetQueueWaitTimeout(GroqSettings settings)
        {
            return TimeSpan.FromSeconds(Math.Clamp(settings.QueueWaitTimeoutSeconds, 1, 60));
        }

        private async Task<SemaphoreSlim?> AcquireSlotAsync(GroqSettings settings, CancellationToken cancellationToken)
        {
            var concurrencyLimiter = GetConcurrencyLimiter(settings);
            using var queueCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            queueCts.CancelAfter(GetQueueWaitTimeout(settings));

            try
            {
                await concurrencyLimiter.WaitAsync(queueCts.Token);
                return concurrencyLimiter;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        private bool TryGetCachedSummary(GroqSettings settings, string cacheKey, out AiSummaryResult result)
        {
            result = new AiSummaryResult();
            if (!settings.EnableResponseCache)
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

        private void TrySetCachedSummary(GroqSettings settings, string cacheKey, AiSummaryResult result)
        {
            if (!settings.EnableResponseCache)
            {
                return;
            }

            var responseCacheDuration = TimeSpan.FromDays(
                Math.Clamp(ResolveResponseCacheDays(settings), 1, 30));
            _cache.Set(cacheKey, CloneSummaryResult(result), responseCacheDuration);
        }

        private static int ResolveResponseCacheDays(GroqSettings settings)
        {
            if (settings.ResponseCacheDays > 0)
            {
                return settings.ResponseCacheDays;
            }

            if (settings.ResponseCacheMinutes > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(settings.ResponseCacheMinutes / 1440d));
            }

            return 7;
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
