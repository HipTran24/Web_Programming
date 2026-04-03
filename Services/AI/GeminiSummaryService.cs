using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Web_Project.Models;

namespace Web_Project.Services.AI
{
    public class GeminiSummaryService
    {
        private readonly HttpClient _httpClient;
        private readonly IAiRuntimeSettingsService _runtimeSettings;
        private readonly ILogger<GeminiSummaryService> _logger;

        public GeminiSummaryService(
            HttpClient httpClient,
            IAiRuntimeSettingsService runtimeSettings,
            ILogger<GeminiSummaryService> logger)
        {
            _httpClient = httpClient;
            _runtimeSettings = runtimeSettings;
            _logger = logger;
        }

        public async Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Gemini;
            EnsureGeminiApiKey(settings);

            var normalized = NormalizeInputText(text, settings.MaxInputCharacters);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("Nội dung văn bản sau xử lý đang rỗng.");
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

            var generated = await GenerateContentAsync(
                settings: settings,
                model: settings.TextModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            return ParseSummaryContent(generated);
        }

        public async Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Gemini;
            EnsureGeminiApiKey(settings);

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
                settings: settings,
                model: settings.VisionModel,
                prompt: prompt,
                temperature: 0.2,
                requireJsonObject: true,
                imageBytes: imageBytes,
                imageMimeType: mimeType,
                cancellationToken: cancellationToken);

            return ParseSummaryContent(generated);
        }

        public Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Gemini backend hiện chưa bật audio transcription trong dự án này.");
        }

        public async Task<AiQuizResult> GenerateQuizAsync(
            string sourceText,
            int totalQuestions,
            string difficulty,
            string quizType,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Gemini;
            EnsureGeminiApiKey(settings);

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
                "TUYET DOI KHONG tạo câu hỏi yêu cầu nhớ mốc thời gian/timestamp trong video (ví dụ: 'ở thời điểm nào', 'tại phút giây nào', 'xuất hiện lúc 00:01:23', 'theo timeline'). " +
                "Chi tao câu hỏi kiểm tra hiểu biết nội dung: khái niệm, bản chất, nguyên lý, quy trình, mục tiêu, so sánh, ứng dụng, kết luận. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"questions\":[{\"questionText\":\"...\",\"optionA\":\"...\",\"optionB\":\"...\",\"optionC\":\"...\",\"optionD\":\"...\",\"correctAnswer\":\"A\",\"explanation\":\"...\"}]}. " +
                "Không thêm markdown, không code fence, không thêm text ngoài JSON.\n\n" +
                $"Nội dung nguồn:\n{normalized}";

            var generated = await GenerateContentAsync(
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

        public async Task<AiPolicyReviewResult> AnalyzePolicyAsync(
            string text,
            string fileName,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            var settings = _runtimeSettings.GetSnapshot().Gemini;
            EnsureGeminiApiKey(settings);

            var normalized = NormalizeInputText(text, settings.MaxInputCharacters > 0
                ? settings.MaxInputCharacters
                : 80_000);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new AiPolicyReviewResult();
            }

            var prompt =
                "Bạn là bộ lọc kiểm duyệt nội dung cho hệ thống SynapLearn. " +
                "Hãy phát hiện xem nội dung có vi phạm chính sách hệ thống hay không. " +
                "Chỉ đánh dấu vi phạm khi nội dung có dấu hiệu cổ vũ, kích động, tuyên truyền phản động, chống phá nhà nước, kêu gọi bất ổn chính trị tiêu cực, thù hằn cực đoan, hoặc tấn công cá nhân theo ngữ cảnh độc hại. " +
                "KHÔNG đánh dấu vi phạm nếu đây chỉ là nội dung học thuật, báo chí trung lập, phân tích lịch sử, trích dẫn có mục đích giáo dục hoặc thảo luận khách quan. " +
                "Bắt buộc trả về JSON hợp lệ đúng định dạng: " +
                "{\"isViolation\":true,\"riskLevel\":\"high\",\"category\":\"anti-state-or-toxic-political\",\"rationale\":\"...\",\"flags\":[\"...\"]}. " +
                "Nếu an toàn, trả về isViolation=false, category='safe', riskLevel='low' và flags=[]. " +
                "Không thêm markdown, không code fence, không thêm text ngoài JSON.\n\n" +
                $"Tên tệp/nguồn: {fileName}\n" +
                $"URL nguồn: {sourceUrl ?? "(không có)"}\n\n" +
                $"Nội dung cần kiểm duyệt:\n{normalized}";

            var generated = await GenerateContentAsync(
                settings: settings,
                model: settings.TextModel,
                prompt: prompt,
                temperature: 0.1,
                requireJsonObject: true,
                imageBytes: null,
                imageMimeType: null,
                cancellationToken: cancellationToken);

            return ParsePolicyContent(generated);
        }

        private async Task<string> GenerateContentAsync(
            GeminiSettings settings,
            string model,
            string prompt,
            double temperature,
            bool requireJsonObject,
            byte[]? imageBytes,
            string? imageMimeType,
            CancellationToken cancellationToken)
        {
            var requestTimeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 120));
            var maxModelCandidates = Math.Clamp(settings.MaxModelCandidates, 1, 5);
            var maxRetriesPerModel = Math.Clamp(settings.MaxRetriesPerModel, 0, 3);
            string? lastError = null;

            foreach (var candidate in ResolveModelCandidates(settings, model, maxModelCandidates))
            {
                for (var attempt = 0; attempt <= maxRetriesPerModel; attempt++)
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        BuildGenerateContentEndpoint(settings, candidate));

                    var payload = BuildGeneratePayload(
                        prompt,
                        temperature,
                        requireJsonObject,
                        imageBytes,
                        imageMimeType);

                    request.Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json");

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
                        lastError = $"Gemini timeout sau {(int)requestTimeout.TotalSeconds}s với model '{candidate}'.";
                        continue;
                    }

                    using (response)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return ExtractGeneratedText(responseBody);
                        }

                        lastError = BuildProviderErrorMessage(response.StatusCode, responseBody, candidate);
                        _logger.LogWarning("Gemini request failed for model {Model}: {Error}", candidate, lastError);

                        if (attempt < maxRetriesPerModel && ShouldRetry(response.StatusCode, responseBody))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                            continue;
                        }

                        if (ShouldTryNextModel(response.StatusCode, responseBody))
                        {
                            break;
                        }

                        throw new InvalidOperationException(lastError);
                    }
                }
            }

            throw new InvalidOperationException(lastError ?? "Không thể xử lý yêu cầu với Gemini.");
        }

        private static object BuildGeneratePayload(
            string prompt,
            double temperature,
            bool requireJsonObject,
            byte[]? imageBytes,
            string? imageMimeType)
        {
            var parts = new List<object>
            {
                new { text = prompt }
            };

            if (imageBytes is { Length: > 0 })
            {
                var mime = string.IsNullOrWhiteSpace(imageMimeType) ? "image/png" : imageMimeType;
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = mime,
                        data = Convert.ToBase64String(imageBytes)
                    }
                });
            }

            var generationConfig = new Dictionary<string, object?>
            {
                ["temperature"] = temperature
            };

            if (requireJsonObject)
            {
                generationConfig["responseMimeType"] = "application/json";
            }

            return new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts
                    }
                },
                generationConfig
            };
        }

        private static string ExtractGeneratedText(string responseBody)
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Phản hồi Gemini không có candidates.");
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var builder = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (!part.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var value = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        builder.AppendLine(value);
                    }
                }

                var text = builder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            throw new InvalidOperationException("Gemini trả về candidates nhưng không có text.");
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

        private static AiPolicyReviewResult ParsePolicyContent(string rawContent)
        {
            var normalized = NormalizeModelPayload(rawContent);

            using var doc = JsonDocument.Parse(normalized);
            var root = doc.RootElement;

            var isViolation = root.TryGetProperty("isViolation", out var violationElement) &&
                violationElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                violationElement.GetBoolean();

            var riskLevel = root.TryGetProperty("riskLevel", out var riskElement)
                ? CleanModelArtifacts(riskElement.GetString())
                : string.Empty;

            var category = root.TryGetProperty("category", out var categoryElement)
                ? CleanModelArtifacts(categoryElement.GetString())
                : string.Empty;

            var rationale = root.TryGetProperty("rationale", out var rationaleElement)
                ? CleanModelArtifacts(rationaleElement.GetString())
                : string.Empty;

            var flags = new List<string>();
            if (root.TryGetProperty("flags", out var flagsElement) &&
                flagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in flagsElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var value = CleanModelArtifacts(item.GetString());
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        flags.Add(value);
                    }
                }
            }

            return new AiPolicyReviewResult
            {
                IsViolation = isViolation,
                RiskLevel = riskLevel,
                Category = category,
                Rationale = rationale,
                Flags = flags
            };
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

        private static string NormalizeModelPayload(string rawContent)
        {
            var normalized = (rawContent ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            normalized = StripCodeFence(normalized);
            normalized = CleanModelArtifacts(normalized);

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

            text = Regex.Replace(text, "^\\s*assistant\\s*:\\s*", string.Empty, RegexOptions.IgnoreCase);
            return text.Trim();
        }

        private static void EnsureGeminiApiKey(GeminiSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Thiếu Gemini:ApiKey. Vui lòng cấu hình key Gemini trước khi gọi endpoint tóm tắt.");
            }
        }

        private static IEnumerable<string> ResolveModelCandidates(GeminiSettings settings, string primaryModel, int maxModelCandidates)
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

        private static string BuildGenerateContentEndpoint(GeminiSettings settings, string model)
        {
            var baseUrl = NormalizeBaseUrl(settings.BaseUrl);
            var encodedModel = Uri.EscapeDataString(NormalizeModel(model));
            var encodedApiKey = Uri.EscapeDataString(settings.ApiKey);
            return $"{baseUrl}/v1beta/models/{encodedModel}:generateContent?key={encodedApiKey}";
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

            var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? "https://generativelanguage.googleapis.com" : normalized;
        }

        private static bool ShouldRetry(System.Net.HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode == System.Net.HttpStatusCode.TooManyRequests ||
                statusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                statusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                statusCode == System.Net.HttpStatusCode.BadGateway ||
                statusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                return true;
            }

            var message = (responseBody ?? string.Empty).ToLowerInvariant();
            return message.Contains("temporarily unavailable") ||
                   message.Contains("overloaded") ||
                   message.Contains("try again later");
        }

        private static bool ShouldTryNextModel(System.Net.HttpStatusCode statusCode, string responseBody)
        {
            if (statusCode == System.Net.HttpStatusCode.NotFound ||
                statusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return true;
            }

            var message = (responseBody ?? string.Empty).ToLowerInvariant();
            return message.Contains("model") && message.Contains("not") && message.Contains("found");
        }

        private static string BuildProviderErrorMessage(System.Net.HttpStatusCode statusCode, string responseBody, string model)
        {
            var providerCode = (int)statusCode;
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

            var normalized = providerMessage.ToLowerInvariant();
            if (providerCode == 429 || normalized.Contains("quota") || normalized.Contains("rate") || normalized.Contains("resource_exhausted"))
            {
                return "Gemini đang vượt quota hoặc tạm quá tải. Vui lòng thử lại sau.";
            }

            if (providerCode == 401 || providerCode == 403 || normalized.Contains("api key") || normalized.Contains("permission"))
            {
                return "Gemini API key không hợp lệ hoặc chưa đủ quyền. Vui lòng kiểm tra cấu hình.";
            }

            if (providerCode == 404 || normalized.Contains("model") && normalized.Contains("not"))
            {
                return $"Model Gemini '{model}' không khả dụng. Hệ thống sẽ thử model dự phòng.";
            }

            return $"Không thể xử lý yêu cầu AI lúc này (mã {providerCode}). Vui lòng thử lại.";
        }
    }
}
