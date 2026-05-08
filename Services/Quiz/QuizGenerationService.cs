using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Web_Project.Models;
using Web_Project.Services.AI;
using Web_Project.Services.Premium;

namespace Web_Project.Services.Quiz
{
    public class QuizGenerationService : IQuizGenerationService
    {
        private static readonly Regex TimecodeRegex = new(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", RegexOptions.Compiled);
        private static readonly Regex TimeQuestionRegex = new(
            @"(mốc thời gian|thời điểm nào|phút(\s*thứ)?\s*\d+|giây(\s*thứ)?\s*\d+|timeline|timestamp|timecode|lúc\s*\d{1,2}:\d{2}(?::\d{2})?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly AppDbContext _dbContext;
        private readonly IGroqSummaryService _groqSummaryService;
        private readonly IUserTokenQuotaService? _tokenQuotaService;

        public QuizGenerationService(
            AppDbContext dbContext,
            IGroqSummaryService groqSummaryService,
            IUserTokenQuotaService? tokenQuotaService = null)
        {
            _dbContext = dbContext;
            _groqSummaryService = groqSummaryService;
            _tokenQuotaService = tokenQuotaService;
        }

        public async Task<GenerateQuizResponse> GenerateQuizAsync(
            GenerateQuizRequest request,
            int? userId,
            string requestIp,
            string userAgent,
            CancellationToken cancellationToken)
        {
            var isGuest = userId is null;
            var normalizedRequestIp = (requestIp ?? string.Empty).Trim();
            var normalizedUserAgent = (userAgent ?? string.Empty).Trim();
            string guestToken = string.Empty;
            GuestSession? guestSession = null;

            if (isGuest)
            {
                guestToken = string.IsNullOrWhiteSpace(request.GuestToken)
                    ? Guid.NewGuid().ToString("N")
                    : request.GuestToken.Trim();

                guestSession = await GetOrCreateGuestSessionAsync(
                    guestToken,
                    normalizedRequestIp,
                    normalizedUserAgent,
                    cancellationToken);

                if (guestSession.TrialUsedAt.HasValue)
                {
                    throw new InvalidOperationException("Guest chỉ được sinh quiz 1 lần. Vui lòng đăng nhập để sử dụng tiếp.");
                }
            }

            var content = await _dbContext.Contents
                .AsNoTracking()
                .Include(x => x.AIProcess)
                .FirstOrDefaultAsync(x => x.ContentId == request.ContentId, cancellationToken);

            if (content is null)
            {
                throw new InvalidOperationException("Không tìm thấy nội dung để sinh quiz.");
            }

            if (isGuest)
            {
                if (content.UserId.HasValue)
                {
                    throw new UnauthorizedAccessException("Guest không thể sinh quiz từ nội dung của tài khoản người dùng.");
                }
            }
                else if (content.UserId.HasValue && content.UserId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền sinh quiz từ nội dung này.");
            }

            var moderation = await _dbContext.ContentModerations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ContentId == content.ContentId, cancellationToken);

            if (moderation is not null &&
                (string.Equals(moderation.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(moderation.Status, "Rejected", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(BuildModerationBlockedMessage(moderation.Status));
            }

            var totalQuestions = ResolveTotalQuestions(request.TotalQuestions);
            var difficulty = NormalizeDifficulty(request.Difficulty);
            var quizType = NormalizeQuizType(request.QuizType);

            var sourceText = BuildQuizSourceText(content);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                throw new InvalidOperationException("Nội dung chưa đủ dữ liệu để sinh câu hỏi trắc nghiệm.");
            }

            if (_tokenQuotaService is not null)
            {
                await _tokenQuotaService.EnsureCanConsumeAsync(
                    userId,
                    _tokenQuotaService.EstimateQuizTokens(sourceText, totalQuestions),
                    "Quiz.Generate",
                    cancellationToken);
            }

            var previousQuestionTexts = await GetPreviousQuestionTextsAsync(
                content.ContentId,
                userId,
                isGuest,
                cancellationToken);

            var previousQuestionSignatures = previousQuestionTexts
                .Select(NormalizeQuestionSignature)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var startedAt = Stopwatch.StartNew();
            try
            {
                var generated = await _groqSummaryService.GenerateQuizAsync(
                    BuildQuizGenerationSource(
                        sourceText,
                        request.VariationNonce,
                        previousQuestionTexts,
                        forceFreshCoverage: false),
                    totalQuestions,
                    difficulty,
                    quizType,
                    cancellationToken);

                var uniqueQuestions = BuildUniqueQuestions(
                    generated.Questions,
                    previousQuestionSignatures,
                    totalQuestions);

                // Ensure quiz size is deterministic for UI/UX: either đủ số câu yêu cầu hoặc báo lỗi rõ ràng.
                const int maxRegenerationAttempts = 3;
                for (var attempt = 0; attempt < maxRegenerationAttempts && uniqueQuestions.Count < totalQuestions; attempt++)
                {
                    var retryNonce = $"{request.VariationNonce}-{Guid.NewGuid():N}";
                    if (retryNonce.Length > 64)
                    {
                        retryNonce = retryNonce[..64];
                    }

                    var regenerationQuestionCount = Math.Min(30, totalQuestions + 5);

                    var retryGenerated = await _groqSummaryService.GenerateQuizAsync(
                        BuildQuizGenerationSource(
                            sourceText,
                            retryNonce,
                            previousQuestionTexts,
                            forceFreshCoverage: true),
                        regenerationQuestionCount,
                        difficulty,
                        quizType,
                        cancellationToken);

                    uniqueQuestions = BuildUniqueQuestions(
                        retryGenerated.Questions,
                        previousQuestionSignatures,
                        totalQuestions,
                        existingQuestions: uniqueQuestions);
                }

                if (uniqueQuestions.Count == 0)
                {
                    throw new InvalidOperationException("AI không sinh được câu hỏi phù hợp từ nội dung này.");
                }

                if (uniqueQuestions.Count < totalQuestions)
                {
                    throw new InvalidOperationException(
                        $"AI chỉ sinh được {uniqueQuestions.Count}/{totalQuestions} câu hợp lệ. Vui lòng bấm 'Reload đề mới' để hệ thống tạo lại đủ số câu.");
                }

                var now = DateTime.UtcNow;
                var quiz = new Models.Quiz
                {
                    ContentId = content.ContentId,
                    UserId = userId,
                    IsGuest = isGuest,
                    TotalQuestions = totalQuestions,
                    Difficulty = difficulty,
                    QuizType = quizType,
                    CreatedAt = now,
                    Questions = uniqueQuestions.Select(MapQuestion).ToList()
                };

                _dbContext.Quizzes.Add(quiz);

                if (isGuest && guestSession is not null)
                {
                    guestSession.TrialUsedAt = now;
                    guestSession.LastSeenAt = now;
                    _dbContext.GuestSessions.Update(guestSession);
                    await IncrementDailyQuizGenerationCounterAsync(
                        userId: null,
                        guestSessionId: guestSession.GuestSessionId,
                        now,
                        cancellationToken);
                }
                else if (userId.HasValue)
                {
                    await IncrementDailyQuizGenerationCounterAsync(
                        userId,
                        guestSessionId: null,
                        now,
                        cancellationToken);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                startedAt.Stop();
                await PersistAiLogAsync("Quiz.Generate", userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: false, cancellationToken);

                return MapQuiz(quiz, includeAnswers: false, guestToken);
            }
            catch
            {
                startedAt.Stop();
                await PersistAiLogAsync("Quiz.Generate", userId, isGuest, startedAt.Elapsed.TotalSeconds, isError: true, cancellationToken);
                throw;
            }
        }

        public async Task<GenerateQuizResponse?> GetQuizAsync(
            int quizId,
            int? userId,
            CancellationToken cancellationToken)
        {
            var quiz = await _dbContext.Quizzes
                .AsNoTracking()
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.QuizId == quizId, cancellationToken);

            if (quiz is null)
            {
                return null;
            }

            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem quiz.");
            }

            if (quiz.UserId.HasValue && quiz.UserId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền truy cập quiz này.");
            }

            return MapQuiz(quiz, includeAnswers: false, guestToken: string.Empty);
        }

        public async Task<GenerateQuizResponse?> GetLatestQuizAsync(
            int? userId,
            CancellationToken cancellationToken)
        {
            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException("Vui lòng đăng nhập để xem quiz.");
            }

            var quiz = await _dbContext.Quizzes
                .AsNoTracking()
                .Include(x => x.Questions)
                .Where(x => x.UserId == userId && !x.IsGuest)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (quiz is null)
            {
                return null;
            }

            return MapQuiz(quiz, includeAnswers: false, guestToken: string.Empty);
        }

        public async Task<SubmitQuizResponse> SubmitQuizAsync(
            SubmitQuizRequest request,
            int? userId,
            CancellationToken cancellationToken)
        {
            var quiz = await _dbContext.Quizzes
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.QuizId == request.QuizId, cancellationToken);

            if (quiz is null)
            {
                throw new InvalidOperationException("Không tìm thấy quiz để nộp bài.");
            }

            if (quiz.IsGuest)
            {
                if (userId.HasValue)
                {
                    throw new UnauthorizedAccessException("Quiz guest không thuộc ngữ cảnh tài khoản đã đăng nhập.");
                }

                if (string.IsNullOrWhiteSpace(request.GuestToken))
                {
                    throw new UnauthorizedAccessException("Thiếu guest token để nộp bài.");
                }

                var session = await _dbContext.GuestSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.GuestToken == request.GuestToken, cancellationToken);
                if (session is null || session.IsBlocked || !session.TrialUsedAt.HasValue)
                {
                    throw new UnauthorizedAccessException("Guest token không hợp lệ hoặc đã hết hiệu lực.");
                }

                var alreadySubmitted = await _dbContext.QuizAttempts
                    .AsNoTracking()
                    .AnyAsync(x => x.QuizId == quiz.QuizId && x.UserId == null, cancellationToken);
                if (alreadySubmitted)
                {
                    throw new InvalidOperationException("Guest chỉ được chấm điểm 1 lần. Vui lòng đăng nhập để tiếp tục.");
                }
            }
            else
            {
                if (!userId.HasValue)
                {
                    throw new UnauthorizedAccessException("Vui lòng đăng nhập để nộp bài.");
                }

                if (quiz.UserId.HasValue && quiz.UserId != userId)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền nộp bài cho quiz này.");
                }
            }

            var answerLookup = (request.Answers ?? [])
                .GroupBy(x => x.QuestionId)
                .ToDictionary(
                    x => x.Key,
                    x => NormalizeSelectedAnswer(x.Last().SelectedAnswer));

            var total = quiz.Questions.Count;
            if (total == 0)
            {
                throw new InvalidOperationException("Quiz không có câu hỏi để chấm điểm.");
            }

            var now = DateTime.UtcNow;
            var attempt = new QuizAttempt
            {
                QuizId = quiz.QuizId,
                UserId = userId,
                StartedAt = now,
                SubmittedAt = now,
                Score = 0,
                UserAnswers = []
            };

            var wrongDetails = new List<WrongQuestionDetailResponse>();
            var correctCount = 0;

            foreach (var question in quiz.Questions.OrderBy(x => x.QuestionId))
            {
                var selected = answerLookup.TryGetValue(question.QuestionId, out var selectedAnswer)
                    ? selectedAnswer
                    : string.Empty;

                var correctAnswer = NormalizeSelectedAnswer(question.CorrectAnswer);
                var isCorrect = !string.IsNullOrWhiteSpace(selected) && selected == correctAnswer;
                if (isCorrect)
                {
                    correctCount += 1;
                }
                else
                {
                    wrongDetails.Add(new WrongQuestionDetailResponse
                    {
                        QuestionId = question.QuestionId,
                        QuestionText = question.QuestionText,
                        SelectedAnswer = string.IsNullOrWhiteSpace(selected) ? "(bỏ trống)" : selected,
                        CorrectAnswer = correctAnswer,
                        Explanation = string.IsNullOrWhiteSpace(question.Explanation)
                            ? BuildFallbackExplanation(question, correctAnswer)
                            : question.Explanation
                    });
                }

                attempt.UserAnswers.Add(new UserAnswer
                {
                    QuestionId = question.QuestionId,
                    SelectedAnswer = selected,
                    IsCorrect = isCorrect
                });
            }

            var wrongCount = total - correctCount;
            var score = Math.Round((double)correctCount * 10d / total, 2);
            attempt.Score = score;

            _dbContext.QuizAttempts.Add(attempt);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new SubmitQuizResponse
            {
                QuizId = quiz.QuizId,
                AttemptId = attempt.AttemptId,
                TotalQuestions = total,
                CorrectCount = correctCount,
                WrongCount = wrongCount,
                Score = score,
                WrongQuestions = wrongDetails
            };
        }

        private static string BuildQuizGenerationSource(
            string sourceText,
            string? variationNonce,
            IReadOnlyList<string> previousQuestions,
            bool forceFreshCoverage)
        {
            var nonce = string.IsNullOrWhiteSpace(variationNonce)
                ? Guid.NewGuid().ToString("N")
                : variationNonce.Trim();

            var sb = new StringBuilder();
            sb.AppendLine(sourceText);
            sb.AppendLine();
            sb.AppendLine($"MA PHIEN DE: {nonce}");

            if (previousQuestions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("CAC CAU HOI DA TUNG DUNG (CAM LAP LAI):");
                foreach (var question in previousQuestions.Take(24))
                {
                    sb.AppendLine($"- {question}");
                }
            }

            if (forceFreshCoverage)
            {
                sb.AppendLine();
                sb.AppendLine("YEU CAU BAT BUOC: BO DE MOI PHAI TAP TRUNG VAO CAC Y/CHU DE KHAC VOI DANH SACH CAM LAP O TREN. KHONG DOI CAU CHU DE CHE GIAU NOI DUNG GIONG NHAU.");
            }

            return sb.ToString();
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
                ActionType = actionType,
                UserId = userId,
                IsGuest = isGuest,
                ProcessingTime = Math.Max(0, processingTimeSeconds),
                IsError = isError,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string BuildModerationBlockedMessage(string status)
        {
            return string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)
                ? "Nội dung này đã bị từ chối sau bước kiểm duyệt nên không thể dùng để sinh quiz."
                : "Nội dung này đang chờ admin duyệt vì có dấu hiệu nhạy cảm. Bạn chưa thể sinh quiz cho tới khi được phê duyệt.";
        }

        private async Task<List<string>> GetPreviousQuestionTextsAsync(
            int contentId,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken)
        {
            if (isGuest || !userId.HasValue)
            {
                return [];
            }

            return await _dbContext.Quizzes
                .AsNoTracking()
                .Where(x => x.ContentId == contentId && x.UserId == userId.Value && !x.IsGuest)
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .SelectMany(x => x.Questions.Select(q => q.QuestionText))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Take(40)
                .ToListAsync(cancellationToken);
        }

        private static List<AiQuizQuestion> BuildUniqueQuestions(
            IEnumerable<AiQuizQuestion> incoming,
            HashSet<string> forbiddenSignatures,
            int maxCount,
            IReadOnlyList<AiQuizQuestion>? existingQuestions = null)
        {
            var output = existingQuestions is null ? new List<AiQuizQuestion>() : [..existingQuestions];
            var seen = output
                .Select(x => NormalizeQuestionSignature(x.QuestionText))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var question in incoming)
            {
                if (output.Count >= maxCount)
                {
                    break;
                }

                var signature = NormalizeQuestionSignature(question.QuestionText);
                if (string.IsNullOrWhiteSpace(signature))
                {
                    continue;
                }

                if (IsTimelineFocusedQuestion(question))
                {
                    continue;
                }

                if (forbiddenSignatures.Contains(signature) || seen.Contains(signature))
                {
                    continue;
                }

                seen.Add(signature);
                output.Add(question);
            }

            return output;
        }

        private static string NormalizeQuestionSignature(string value)
        {
            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var noDiacritics = RemoveDiacritics(raw).ToLowerInvariant();
            var compact = Regex.Replace(noDiacritics, "[^a-z0-9]+", " ").Trim();
            return compact;
        }

        private static string RemoveDiacritics(string text)
        {
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

        private static string NormalizeSelectedAnswer(string raw)
        {
            var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
            return value is "A" or "B" or "C" or "D" ? value : string.Empty;
        }

        private static string BuildFallbackExplanation(Question question, string correctAnswer)
        {
            var option = correctAnswer switch
            {
                "A" => question.OptionA,
                "B" => question.OptionB,
                "C" => question.OptionC,
                "D" => question.OptionD,
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(option))
            {
                return "Đáp án đúng dựa trên nội dung đã tóm tắt từ tài liệu nguồn.";
            }

            return $"Đáp án đúng là {correctAnswer} vì phù hợp nhất với nội dung nguồn: {option}.";
        }

        private async Task<GuestSession> GetOrCreateGuestSessionAsync(
            string guestToken,
            string requestIp,
            string userAgent,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var normalizedIp = TrimTo(requestIp, 64);
            var normalizedUserAgent = TrimTo(userAgent, 512);
            var normalizedToken = TrimTo(guestToken, 128);
            var fingerprintHash = BuildFingerprintHash(normalizedIp, normalizedUserAgent);

            var session = await _dbContext.GuestSessions
                .FirstOrDefaultAsync(x => x.GuestToken == normalizedToken, cancellationToken);

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

                if (!string.IsNullOrWhiteSpace(normalizedToken))
                {
                    session.GuestToken = normalizedToken;
                }

                if (!string.IsNullOrWhiteSpace(fingerprintHash))
                {
                    session.FingerprintHash = fingerprintHash;
                }

                session.LastSeenAt = now;
                if (!string.IsNullOrWhiteSpace(normalizedIp))
                {
                    session.IpAddress = normalizedIp;
                }

                if (!string.IsNullOrWhiteSpace(normalizedUserAgent))
                {
                    session.UserAgent = normalizedUserAgent;
                }

                return session;
            }

            session = new GuestSession
            {
                GuestToken = normalizedToken,
                FingerprintHash = fingerprintHash,
                IpAddress = normalizedIp,
                UserAgent = normalizedUserAgent,
                FirstSeenAt = now,
                LastSeenAt = now,
                TrialUsedAt = null,
                IsBlocked = false
            };

            _dbContext.GuestSessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return session;
        }

        private async Task IncrementDailyQuizGenerationCounterAsync(
            int? userId,
            int? guestSessionId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var usageDate = now.Date;
            DailyUsageCounter? counter;

            if (userId.HasValue)
            {
                counter = await _dbContext.DailyUsageCounters
                    .FirstOrDefaultAsync(
                        x => x.UserId == userId && x.UsageDate == usageDate,
                        cancellationToken);
            }
            else
            {
                counter = await _dbContext.DailyUsageCounters
                    .FirstOrDefaultAsync(
                        x => x.GuestSessionId == guestSessionId && x.UsageDate == usageDate,
                        cancellationToken);
            }

            if (counter is null)
            {
                counter = new DailyUsageCounter
                {
                    UsageDate = usageDate,
                    UserId = userId,
                    GuestSessionId = guestSessionId,
                    UploadCount = 0,
                    AIProcessCount = 0,
                    QuizGenerationCount = 0,
                    TotalProcessingTime = 0,
                    UpdatedAt = now
                };

                _dbContext.DailyUsageCounters.Add(counter);
            }

            counter.QuizGenerationCount += 1;
            counter.UpdatedAt = now;
        }

        private static string TrimTo(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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

        private static string BuildQuizSourceText(Models.Content content)
        {
            var summary = RemoveTimecodes(content.AIProcess?.Summary?.Trim() ?? string.Empty);
            var keyPoints = FilterTimelineKeyPoints(content.AIProcess?.KeyPoints?.Trim() ?? string.Empty);
            var extracted = RemoveTimecodes(content.ExtractedText?.Trim() ?? string.Empty);

            var source = string.Empty;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                source += $"TOM TAT:\n{summary}\n\n";
            }

            if (!string.IsNullOrWhiteSpace(keyPoints))
            {
                source += $"Y CHINH:\n{keyPoints}\n\n";
            }

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                var slice = extracted.Length > 12000 ? extracted[..12000] : extracted;
                source += $"NOI DUNG GOC (RUT GON):\n{slice}";
            }

            return source.Trim();
        }

        private static bool IsTimelineFocusedQuestion(AiQuizQuestion question)
        {
            var questionText = question.QuestionText ?? string.Empty;
            var explanation = question.Explanation ?? string.Empty;
            return TimeQuestionRegex.IsMatch(questionText) || TimeQuestionRegex.IsMatch(explanation);
        }

        private static string FilterTimelineKeyPoints(string rawKeyPoints)
        {
            if (string.IsNullOrWhiteSpace(rawKeyPoints))
            {
                return string.Empty;
            }

            var lines = rawKeyPoints
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line =>
                {
                    var noDiacritics = RemoveDiacritics(line).ToLowerInvariant();
                    if (noDiacritics.Contains("moc thoi gian") || noDiacritics.Contains("timeline"))
                    {
                        return false;
                    }

                    return !TimecodeRegex.IsMatch(line);
                })
                .ToList();

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static string RemoveTimecodes(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            return TimecodeRegex.Replace(raw, string.Empty).Trim();
        }

        private static int ResolveTotalQuestions(int? requested)
        {
            var value = requested ?? 10;
            if (value <= 0)
            {
                value = 10;
            }

            return Math.Clamp(value, 1, 30);
        }

        private static string NormalizeDifficulty(string? rawDifficulty)
        {
            var normalized = (rawDifficulty ?? "medium").Trim().ToLowerInvariant();
            return normalized switch
            {
                "easy" or "de" => "easy",
                "hard" or "kho" => "hard",
                _ => "medium"
            };
        }

        private static string NormalizeQuizType(string? rawQuizType)
        {
            var normalized = (rawQuizType ?? "multiple-choice").Trim().ToLowerInvariant();
            return normalized switch
            {
                "flashcard" => "flashcard",
                _ => "multiple-choice"
            };
        }

        private static Models.Question MapQuestion(AiQuizQuestion source)
        {
            return new Models.Question
            {
                QuestionText = source.QuestionText,
                OptionA = source.OptionA,
                OptionB = source.OptionB,
                OptionC = source.OptionC,
                OptionD = source.OptionD,
                CorrectAnswer = source.CorrectAnswer,
                Explanation = source.Explanation
            };
        }

        private static GenerateQuizResponse MapQuiz(Models.Quiz quiz, bool includeAnswers, string guestToken)
        {
            return new GenerateQuizResponse
            {
                QuizId = quiz.QuizId,
                ContentId = quiz.ContentId,
                TotalQuestions = quiz.TotalQuestions,
                Difficulty = quiz.Difficulty,
                QuizType = quiz.QuizType,
                IsGuest = quiz.IsGuest,
                CreatedAt = quiz.CreatedAt,
                GuestToken = guestToken,
                Questions = quiz.Questions
                    .OrderBy(x => x.QuestionId)
                    .Select(x => new GeneratedQuestionResponse
                    {
                        QuestionId = x.QuestionId,
                        QuestionText = x.QuestionText,
                        OptionA = x.OptionA,
                        OptionB = x.OptionB,
                        OptionC = x.OptionC,
                        OptionD = x.OptionD,
                        CorrectAnswer = includeAnswers ? x.CorrectAnswer : null,
                        Explanation = includeAnswers ? x.Explanation : null
                    })
                    .ToList()
            };
        }
    }
}
