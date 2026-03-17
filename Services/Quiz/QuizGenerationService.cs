using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Web_Project.Models;
using Web_Project.Services.AI;

namespace Web_Project.Services.Quiz
{
    public class QuizGenerationService : IQuizGenerationService
    {

        private readonly AppDbContext _dbContext;
        private readonly IGeminiSummaryService _geminiSummaryService;

        public QuizGenerationService(
            AppDbContext dbContext,
            IGeminiSummaryService geminiSummaryService)
        {
            _dbContext = dbContext;
            _geminiSummaryService = geminiSummaryService;
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

                var totalQuestions = ResolveTotalQuestions(request.TotalQuestions);
            var difficulty = NormalizeDifficulty(request.Difficulty);
            var quizType = NormalizeQuizType(request.QuizType);

            var sourceText = BuildQuizSourceText(content);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                throw new InvalidOperationException("Nội dung chưa đủ dữ liệu để sinh câu hỏi trắc nghiệm.");
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

            var generated = await _geminiSummaryService.GenerateQuizAsync(
                BuildQuizGenerationSource(sourceText, request.VariationNonce, previousQuestionTexts, forceFreshCoverage: false),
                totalQuestions,
                difficulty,
                quizType,
                cancellationToken);

            var uniqueQuestions = BuildUniqueQuestions(
                generated.Questions,
                previousQuestionSignatures,
                totalQuestions);

            var minTarget = Math.Max(3, totalQuestions * 7 / 10);
            if (uniqueQuestions.Count < minTarget)
            {
                var retryNonce = $"{request.VariationNonce}-{Guid.NewGuid():N}";
                if (retryNonce.Length > 64)
                {
                    retryNonce = retryNonce[..64];
                }

                var retryGenerated = await _geminiSummaryService.GenerateQuizAsync(
                    BuildQuizGenerationSource(
                        sourceText,
                        retryNonce,
                        previousQuestionTexts,
                        forceFreshCoverage: true),
                    totalQuestions,
                    difficulty,
                    quizType,
                    cancellationToken);

                var retryUnique = BuildUniqueQuestions(
                    retryGenerated.Questions,
                    previousQuestionSignatures,
                    totalQuestions,
                    existingQuestions: uniqueQuestions);

                uniqueQuestions = retryUnique;
            }

            if (uniqueQuestions.Count == 0)
            {
                throw new InvalidOperationException("AI không sinh được câu hỏi phù hợp từ nội dung này.");
            }

            var now = DateTime.UtcNow;
            var quiz = new Models.Quiz
            {
                ContentId = content.ContentId,
                UserId = userId,
                IsGuest = isGuest,
                TotalQuestions = uniqueQuestions.Count,
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

            return MapQuiz(quiz, includeAnswers: false, guestToken);
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
            var session = await _dbContext.GuestSessions
                .FirstOrDefaultAsync(x => x.GuestToken == guestToken, cancellationToken);

            if (session is not null)
            {
                if (session.IsBlocked)
                {
                    throw new InvalidOperationException("Guest session đã bị chặn. Vui lòng đăng nhập để tiếp tục.");
                }

                session.LastSeenAt = now;
                if (!string.IsNullOrWhiteSpace(requestIp))
                {
                    session.IpAddress = TrimTo(requestIp, 64);
                }

                if (!string.IsNullOrWhiteSpace(userAgent))
                {
                    session.UserAgent = TrimTo(userAgent, 512);
                }

                return session;
            }

            session = new GuestSession
            {
                GuestToken = TrimTo(guestToken, 128),
                FingerprintHash = string.Empty,
                IpAddress = TrimTo(requestIp, 64),
                UserAgent = TrimTo(userAgent, 512),
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

        private static string BuildQuizSourceText(Models.Content content)
        {
            var summary = content.AIProcess?.Summary?.Trim() ?? string.Empty;
            var keyPoints = content.AIProcess?.KeyPoints?.Trim() ?? string.Empty;
            var extracted = content.ExtractedText?.Trim() ?? string.Empty;

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
