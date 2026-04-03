using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "UserOnly")]
    public class DashboardController : ControllerBase
    {
        private const int WeeklyGoalSessions = 7;
        private readonly AppDbContext _dbContext;

        public DashboardController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users
                .Where(x => x.UserId == userId.Value)
                .Select(x => new
                {
                    x.UserId,
                    x.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            var previousWeekStart = now.AddDays(-14);

            var totalContents = await _dbContext.Contents
                .CountAsync(x => x.UserId == userId.Value && !x.IsGuest, cancellationToken);

            var totalContentsLast7Days = await _dbContext.Contents
                .CountAsync(
                    x => x.UserId == userId.Value &&
                         !x.IsGuest &&
                         x.CreatedAt >= sevenDaysAgo,
                    cancellationToken);

            var totalAttempts = await _dbContext.QuizAttempts
                .CountAsync(x => x.UserId == userId.Value, cancellationToken);

            var recentAverageScore = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.SubmittedAt)
                .Take(20)
                .Select(x => (double?)x.Score)
                .AverageAsync(cancellationToken) ?? 0d;

            var activeSessionDatesCurrentWeek = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value && x.SubmittedAt >= sevenDaysAgo)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            var activeSessionDatesPreviousWeek = await _dbContext.QuizAttempts
                .Where(
                    x => x.UserId == userId.Value &&
                         x.SubmittedAt >= previousWeekStart &&
                         x.SubmittedAt < sevenDaysAgo)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            var weeklyGoalPercent = Math.Round(
                Math.Min(100d, activeSessionDatesCurrentWeek * 100d / WeeklyGoalSessions),
                0,
                MidpointRounding.AwayFromZero);

            var recentContents = await _dbContext.Contents
                .Where(x => x.UserId == userId.Value && !x.IsGuest)
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
                .Select(x => new
                {
                    x.ContentId,
                    x.FileName,
                    x.SourceType,
                    x.CreatedAt,
                    HasAiProcess = x.AIProcess != null
                })
                .ToListAsync(cancellationToken);

            var recentAttempts = await (
                    from attempt in _dbContext.QuizAttempts
                    join quiz in _dbContext.Quizzes on attempt.QuizId equals quiz.QuizId
                    join content in _dbContext.Contents on quiz.ContentId equals content.ContentId into contentJoin
                    from content in contentJoin.DefaultIfEmpty()
                    where attempt.UserId == userId.Value
                    orderby attempt.SubmittedAt descending
                    select new
                    {
                        attempt.QuizId,
                        attempt.Score,
                        attempt.SubmittedAt,
                        ContentName = content != null ? content.FileName : null
                    })
                .Take(10)
                .ToListAsync(cancellationToken);

            var continueItem = recentContents
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            var weakTopicRows = await (
                    from attempt in _dbContext.QuizAttempts
                    join quiz in _dbContext.Quizzes on attempt.QuizId equals quiz.QuizId
                    join content in _dbContext.Contents on quiz.ContentId equals content.ContentId into contentJoin
                    from content in contentJoin.DefaultIfEmpty()
                    where attempt.UserId == userId.Value && !string.IsNullOrWhiteSpace(content != null ? content.AI_DetectedSubject : null)
                    group attempt by content!.AI_DetectedSubject into g
                    orderby g.Average(t => t.Score), g.Count() descending
                    select new
                    {
                        Topic = g.Key,
                        Avg = g.Average(t => t.Score),
                        Attempts = g.Count()
                    })
                .Take(3)
                .ToListAsync(cancellationToken);

            var weakTopics = weakTopicRows
                .Select(x => new
                {
                    name = NormalizeTopicLabel(x.Topic),
                    accuracyPercent = ClampPercent(x.Avg * 10d)
                })
                .ToList();

            if (weakTopics.Count == 0)
            {
                var studyWeakTopic = await _dbContext.StudyStatistics
                    .Where(x => x.UserId == userId.Value)
                    .Select(x => x.WeakTopic)
                    .FirstOrDefaultAsync(cancellationToken);

                var fallbackTopics = SplitWeakTopics(studyWeakTopic)
                    .Take(3)
                    .Select((topic, idx) => new
                    {
                        name = topic,
                        accuracyPercent = Math.Max(35, 60 - (idx * 8))
                    });

                weakTopics.AddRange(fallbackTopics);
            }

            var activityItems = recentContents
                .Select(x => new
                {
                    at = x.CreatedAt,
                    title = NormalizeTitle(x.FileName),
                    kind = x.SourceType,
                    result = x.HasAiProcess ? "AI hoàn tất" : "Đang xử lý",
                    actionText = "Mở chi tiết",
                    actionUrl = $"/home/content-detail.html?contentId={x.ContentId}"
                })
                .Concat(recentAttempts.Select(x => new
                {
                    at = x.SubmittedAt,
                    title = NormalizeTitle(x.ContentName),
                    kind = "Quiz",
                    result = $"{Math.Round(x.Score, 1, MidpointRounding.AwayFromZero):0.#}/10",
                    actionText = "Ôn lại",
                    actionUrl = "/home/quiz-result.html"
                }))
                .OrderByDescending(x => x.at)
                .Take(6)
                .ToList();

            var firstName = ResolveFirstName(user.FullName);
            var streakDelta = activeSessionDatesCurrentWeek - activeSessionDatesPreviousWeek;

            return Ok(new
            {
                greetingName = firstName,
                streakDays = activeSessionDatesCurrentWeek,
                streakDelta,
                kpis = new
                {
                    totalContents,
                    totalContentsLast7Days,
                    completedQuizzes = totalAttempts,
                    weeklyGoalPercent,
                    averageScoreRecent = Math.Round(recentAverageScore, 1, MidpointRounding.AwayFromZero)
                },
                continueLearning = continueItem is null
                    ? null
                    : new
                    {
                        title = NormalizeTitle(continueItem.FileName),
                        updatedAt = continueItem.CreatedAt,
                        hasAiProcess = continueItem.HasAiProcess,
                        viewUrl = $"/home/content-detail.html?contentId={continueItem.ContentId}",
                        quizUrl = "/home/upload.html"
                    },
                recentActivities = activityItems,
                weakTopics,
                suggestions = BuildSuggestions(weakTopics.Count, totalAttempts, continueItem is not null)
            });
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics(CancellationToken cancellationToken)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var userExists = await _dbContext.Users
                .AnyAsync(x => x.UserId == userId.Value, cancellationToken);

            if (!userExists)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var now = DateTime.UtcNow;
            var today = now.Date;
            var sevenDaysAgo = today.AddDays(-6);
            var previousSevenDaysStart = today.AddDays(-13);

            var totalAttempts = await _dbContext.QuizAttempts
                .CountAsync(x => x.UserId == userId.Value, cancellationToken);

            var attemptsLast7Days = await _dbContext.QuizAttempts
                .CountAsync(
                    x => x.UserId == userId.Value && x.SubmittedAt >= sevenDaysAgo,
                    cancellationToken);

            var activeDaysCurrentWeek = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value && x.SubmittedAt >= sevenDaysAgo)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            var activeDaysPreviousWeek = await _dbContext.QuizAttempts
                .Where(
                    x => x.UserId == userId.Value &&
                         x.SubmittedAt >= previousSevenDaysStart &&
                         x.SubmittedAt < sevenDaysAgo)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            var averageScoreRecentRaw = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.SubmittedAt)
                .Take(20)
                .Select(x => (double?)x.Score)
                .AverageAsync(cancellationToken) ?? 0d;

            var averageScorePreviousPeriodRaw = await _dbContext.QuizAttempts
                .Where(
                    x => x.UserId == userId.Value &&
                         x.SubmittedAt >= previousSevenDaysStart &&
                         x.SubmittedAt < sevenDaysAgo)
                .Select(x => (double?)x.Score)
                .AverageAsync(cancellationToken) ?? 0d;

            var averageScoreRecentPercent = ClampPercent(averageScoreRecentRaw * 10d);
            var averageScoreDeltaPercent = Math.Round(
                (averageScoreRecentRaw - averageScorePreviousPeriodRaw) * 10d,
                1,
                MidpointRounding.AwayFromZero);

            var wrongAnswersCount = await _dbContext.UserAnswers
                .Where(x => !x.IsCorrect && x.QuizAttempt.UserId == userId.Value)
                .CountAsync(cancellationToken);

            var worstTopicRows = await (
                    from attempt in _dbContext.QuizAttempts
                    join quiz in _dbContext.Quizzes on attempt.QuizId equals quiz.QuizId
                    join content in _dbContext.Contents on quiz.ContentId equals content.ContentId into contentJoin
                    from content in contentJoin.DefaultIfEmpty()
                    where attempt.UserId == userId.Value && !string.IsNullOrWhiteSpace(content != null ? content.AI_DetectedSubject : null)
                    group attempt by content!.AI_DetectedSubject into g
                    orderby g.Average(t => t.Score), g.Count() descending
                    select new
                    {
                        Topic = g.Key,
                        Avg = g.Average(t => t.Score),
                        Attempts = g.Count()
                    })
                .Take(3)
                .ToListAsync(cancellationToken);

            var topicAccuracy = worstTopicRows
                .Select(x => new
                {
                    topic = x.Topic,
                    accuracyPercent = ClampPercent(x.Avg * 10d),
                    attempts = x.Attempts
                })
                .ToList();

            var dailyRows = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value && x.SubmittedAt >= sevenDaysAgo)
                .GroupBy(x => x.SubmittedAt.Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Avg = g.Average(t => t.Score)
                })
                .ToListAsync(cancellationToken);
            var dailyRowsByDay = dailyRows.ToDictionary(x => x.Day, x => x);

            var dayLabels = new[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
            var dailyTrend = Enumerable
                .Range(0, 7)
                .Select(offset =>
                {
                    var day = sevenDaysAgo.AddDays(offset);
                    dailyRowsByDay.TryGetValue(day, out var match);
                    var percent = match is null ? 0 : ClampPercent(match.Avg * 10d);
                    var dayIndex = ((int)day.DayOfWeek + 6) % 7;

                    return new
                    {
                        day = dayLabels[dayIndex],
                        scorePercent = percent,
                        hasData = match is not null
                    };
                })
                .ToList();

            var topWrongQuestions = await _dbContext.UserAnswers
                .Where(x => !x.IsCorrect && x.QuizAttempt.UserId == userId.Value)
                .GroupBy(x => new
                {
                    x.QuestionId,
                    x.Question.QuestionText,
                    Topic = x.Question.Quiz.Content.AI_DetectedSubject
                })
                .Select(g => new
                {
                    questionId = g.Key.QuestionId,
                    questionText = g.Key.QuestionText,
                    topic = string.IsNullOrWhiteSpace(g.Key.Topic) ? "Khác" : g.Key.Topic,
                    wrongCount = g.Count()
                })
                .OrderByDescending(x => x.wrongCount)
                .ThenBy(x => x.questionId)
                .Take(8)
                .ToListAsync(cancellationToken);

            var consistencyLabel = BuildConsistencyLabel(activeDaysCurrentWeek);
            var focusTopics = topicAccuracy.Take(2).Select(x => x.topic).ToList();

            return Ok(new
            {
                kpis = new
                {
                    averageScorePercent = averageScoreRecentPercent,
                    averageScoreDeltaPercent,
                    totalAttempts,
                    attemptsLast7Days,
                    wrongAnswersCount,
                    consistencyLabel,
                    activeDaysCurrentWeek,
                    activeDaysPreviousWeek
                },
                dailyTrend,
                topicAccuracy,
                topWrongQuestions,
                suggestions = BuildAnalyticsSuggestions(focusTopics, wrongAnswersCount, attemptsLast7Days),
                lastUpdatedAt = now
            });
        }

        [HttpGet("history-activities")]
        public async Task<IActionResult> GetHistoryActivities(
            [FromQuery] string? filter,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var normalizedFilter = string.Equals(filter, "quiz", StringComparison.OrdinalIgnoreCase)
                ? "quiz"
                : string.Equals(filter, "content", StringComparison.OrdinalIgnoreCase)
                    ? "content"
                    : "all";

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 100);
            var takeLimit = safePage * safePageSize;

            var contentCount = await _dbContext.Contents
                .Where(x => x.UserId == userId.Value && !x.IsGuest)
                .CountAsync(cancellationToken);

            var quizCount = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value)
                .CountAsync(cancellationToken);

            var contentItems = await _dbContext.Contents
                .Where(x => x.UserId == userId.Value && !x.IsGuest)
                .OrderByDescending(x => x.CreatedAt)
                .Take(takeLimit)
                .Select(x => new HistoryActivityCandidate
                {
                    At = x.CreatedAt,
                    Title = x.FileName,
                    Kind = x.SourceType,
                    Result = x.AIProcess != null ? "AI hoàn tất" : "Đang xử lý",
                    IsQuiz = false,
                    ContentId = x.ContentId,
                    QuizId = null,
                })
                .ToListAsync(cancellationToken);

            var quizItems = await (
                    from attempt in _dbContext.QuizAttempts
                    join quiz in _dbContext.Quizzes on attempt.QuizId equals quiz.QuizId
                    join content in _dbContext.Contents on quiz.ContentId equals content.ContentId into contentJoin
                    from content in contentJoin.DefaultIfEmpty()
                    where attempt.UserId == userId.Value
                    orderby attempt.SubmittedAt descending
                    select new HistoryActivityCandidate
                    {
                        At = attempt.SubmittedAt,
                        Title = content != null ? content.FileName : $"Quiz #{attempt.QuizId}",
                        Kind = "Quiz",
                        Result = attempt.Score,
                        IsQuiz = true,
                        ContentId = null,
                        QuizId = attempt.QuizId,
                    })
                .Take(takeLimit)
                .ToListAsync(cancellationToken);
            var totalItems = normalizedFilter switch
            {
                "content" => contentCount,
                "quiz" => quizCount,
                _ => contentCount + quizCount,
            };

            var merged = contentItems
                .Concat(quizItems)
                .Where(x => normalizedFilter == "all" || (normalizedFilter == "quiz" ? x.IsQuiz : !x.IsQuiz))
                .OrderByDescending(x => x.At)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    at = x.At,
                    title = NormalizeTitle(x.Title),
                    kind = x.Kind,
                    result = x.IsQuiz
                        ? $"{Math.Round(Convert.ToDouble(x.Result), 1, MidpointRounding.AwayFromZero):0.#}/10"
                        : Convert.ToString(x.Result) ?? "Đã cập nhật",
                    actionText = x.IsQuiz ? "Ôn lại" : "Mở chi tiết",
                    actionUrl = x.IsQuiz
                        ? "/home/quiz-result.html"
                        : $"/home/content-detail.html?contentId={x.ContentId.GetValueOrDefault()}"
                })
                .ToList();

            return Ok(new
            {
                filter = normalizedFilter,
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items = merged,
            });
        }

        [HttpGet("learning-plan")]
        public async Task<IActionResult> GetLearningPlan(CancellationToken cancellationToken)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var userExists = await _dbContext.Users
                .AnyAsync(x => x.UserId == userId.Value, cancellationToken);

            if (!userExists)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var today = DateTime.UtcNow.Date;
            var weekStart = today.AddDays(-6);

            var totalContentsLast7Days = await _dbContext.Contents
                .CountAsync(
                    x => x.UserId == userId.Value && !x.IsGuest && x.CreatedAt >= weekStart,
                    cancellationToken);

            var totalAttemptsLast7Days = await _dbContext.QuizAttempts
                .CountAsync(
                    x => x.UserId == userId.Value && x.SubmittedAt >= weekStart,
                    cancellationToken);

            var activeDaysCurrentWeek = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value && x.SubmittedAt >= weekStart)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            var recentAverageScore = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.SubmittedAt)
                .Take(20)
                .Select(x => (double?)x.Score)
                .AverageAsync(cancellationToken) ?? 0d;

            var weakTopicRows = await (
                    from attempt in _dbContext.QuizAttempts
                    join quiz in _dbContext.Quizzes on attempt.QuizId equals quiz.QuizId
                    join content in _dbContext.Contents on quiz.ContentId equals content.ContentId into contentJoin
                    from content in contentJoin.DefaultIfEmpty()
                    where attempt.UserId == userId.Value && !string.IsNullOrWhiteSpace(content != null ? content.AI_DetectedSubject : null)
                    group attempt by content!.AI_DetectedSubject into g
                    orderby g.Average(t => t.Score), g.Count() descending
                    select new
                    {
                        Topic = g.Key,
                        AccuracyPercent = ClampPercent(g.Average(t => t.Score) * 10d),
                        Attempts = g.Count(),
                    })
                .Take(3)
                .ToListAsync(cancellationToken);

            var weekProgressPercent = Math.Clamp(
                (int)Math.Round(activeDaysCurrentWeek * 100d / WeeklyGoalSessions, MidpointRounding.AwayFromZero),
                0,
                100);

            var focusTopic = NormalizeTopicLabel(weakTopicRows.FirstOrDefault()?.Topic);

            var tasks = new[]
            {
                new
                {
                    id = "upload-1-content",
                    title = "Xử lý ít nhất 1 nội dung trong tuần",
                    detail = "Upload hoặc tóm tắt 1 tài liệu để duy trì đầu vào học tập.",
                    isCompleted = totalContentsLast7Days >= 1,
                    target = 1,
                    current = totalContentsLast7Days,
                    actionUrl = "/home/upload.html",
                    actionText = "Tạo nội dung mới"
                },
                new
                {
                    id = "finish-3-quiz",
                    title = "Hoàn thành tối thiểu 3 quiz/tuần",
                    detail = "Giữ nhịp ôn luyện đều để hệ thống phân tích chính xác điểm yếu.",
                    isCompleted = totalAttemptsLast7Days >= 3,
                    target = 3,
                    current = totalAttemptsLast7Days,
                    actionUrl = "/home/quiz.html",
                    actionText = "Làm quiz"
                },
                new
                {
                    id = "review-weak-topic",
                    title = "Ôn lại chủ đề yếu nhất",
                    detail = $"Ưu tiên chủ đề {focusTopic} trước khi tăng độ khó.",
                    isCompleted = recentAverageScore >= 7.5,
                    target = 75,
                    current = ClampPercent(recentAverageScore * 10d),
                    actionUrl = "/home/analytics.html",
                    actionText = "Xem phân tích"
                },
                new
                {
                    id = "active-7-days",
                    title = "Đạt mục tiêu 7 ngày hoạt động",
                    detail = "Mỗi ngày làm ít nhất 1 quiz ngắn để hoàn thành mục tiêu tuần.",
                    isCompleted = activeDaysCurrentWeek >= WeeklyGoalSessions,
                    target = WeeklyGoalSessions,
                    current = activeDaysCurrentWeek,
                    actionUrl = "/home/history.html",
                    actionText = "Mở lịch sử"
                }
            };

            var recommendations = BuildLearningPlanRecommendations(
                totalContentsLast7Days,
                totalAttemptsLast7Days,
                activeDaysCurrentWeek,
                focusTopic);

            return Ok(new
            {
                weekStartAt = weekStart,
                weekEndAt = today,
                weeklyGoalSessions = WeeklyGoalSessions,
                activeDaysCurrentWeek,
                weekProgressPercent,
                averageScoreRecent = Math.Round(recentAverageScore, 1, MidpointRounding.AwayFromZero),
                focusTopics = weakTopicRows.Select(x => new
                {
                    name = NormalizeTopicLabel(x.Topic),
                    accuracyPercent = x.AccuracyPercent,
                    attempts = x.Attempts
                }),
                tasks,
                recommendations
            });
        }

        private int? TryGetCurrentUserId()
        {
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdRaw, out var userId) ? userId : null;
        }

        private static int ClampPercent(double value)
        {
            return (int)Math.Clamp(Math.Round(value, 0, MidpointRounding.AwayFromZero), 0d, 100d);
        }

        private static string ResolveFirstName(string? fullName)
        {
            var normalized = string.IsNullOrWhiteSpace(fullName)
                ? "Bạn"
                : fullName.Trim();

            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? normalized : parts[^1];
        }

        private static string NormalizeTitle(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Nội dung chưa đặt tên";
            }

            var trimmed = value.Trim();
            if (trimmed.Length <= 70)
            {
                return trimmed;
            }

            return $"{trimmed[..67]}...";
        }

        private static string NormalizeTopicLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Tổng quan";
            }

            var compact = string.Join(" ", value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (compact.Length == 0)
            {
                return "Tổng quan";
            }

            var firstSentenceBreak = compact.IndexOfAny(new[] { '.', '!', '?', '\n', '\r' });
            if (firstSentenceBreak > 20)
            {
                compact = compact[..firstSentenceBreak].Trim();
            }

            if (compact.Length <= 72)
            {
                return compact;
            }

            return $"{compact[..69]}...";
        }

        private static IEnumerable<string> SplitWeakTopics(string? weakTopic)
        {
            if (string.IsNullOrWhiteSpace(weakTopic))
            {
                return Enumerable.Empty<string>();
            }

            return weakTopic
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> BuildSuggestions(int weakTopicCount, int totalAttempts, bool hasContinueItem)
        {
            var suggestions = new List<string>();

            if (hasContinueItem)
            {
                suggestions.Add("Tiếp tục nội dung gần nhất để duy trì nhịp học hôm nay.");
            }

            if (weakTopicCount > 0)
            {
                suggestions.Add("Ưu tiên ôn các chủ đề có độ chính xác thấp trước khi làm đề mới.");
            }

            if (totalAttempts < 5)
            {
                suggestions.Add("Tạo thêm quiz 10-15 câu để hệ thống nhận diện điểm yếu chính xác hơn.");
            }
            else
            {
                suggestions.Add("Làm lại một quiz điểm thấp để cải thiện tốc độ và độ chính xác.");
            }

            return suggestions;
        }

        private static string BuildConsistencyLabel(int activeDaysCurrentWeek)
        {
            if (activeDaysCurrentWeek >= 5)
            {
                return "Ổn định cao";
            }

            if (activeDaysCurrentWeek >= 3)
            {
                return "Khá ổn định";
            }

            if (activeDaysCurrentWeek >= 1)
            {
                return "Cần cải thiện";
            }

            return "Chưa có dữ liệu";
        }

        private static IReadOnlyList<string> BuildAnalyticsSuggestions(
            IReadOnlyList<string> focusTopics,
            int wrongAnswersCount,
            int attemptsLast7Days)
        {
            var suggestions = new List<string>();

            if (focusTopics.Count > 0)
            {
                suggestions.Add($"Ưu tiên ôn lại chủ đề {focusTopics[0]} trước khi làm đề mới.");
            }

            if (focusTopics.Count > 1)
            {
                suggestions.Add($"Dành thêm 15 phút cho chủ đề {focusTopics[1]} để giảm lỗi lặp lại.");
            }

            if (wrongAnswersCount > 0)
            {
                suggestions.Add($"Bạn đang có {wrongAnswersCount} câu sai cần ôn. Hãy làm lại một quiz ngắn để kiểm tra lại.");
            }

            if (attemptsLast7Days < 3)
            {
                suggestions.Add("Tăng tần suất quiz trong tuần này để hệ thống nhận diện điểm yếu chính xác hơn.");
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add("Tiếp tục duy trì nhịp học hiện tại và tăng dần độ khó của quiz.");
            }

            return suggestions;
        }

        private static IReadOnlyList<object> BuildLearningPlanRecommendations(
            int totalContentsLast7Days,
            int totalAttemptsLast7Days,
            int activeDaysCurrentWeek,
            string focusTopic)
        {
            var items = new List<object>();

            if (totalContentsLast7Days == 0)
            {
                items.Add(new
                {
                    title = "Bắt đầu bằng 1 nội dung mới",
                    detail = "Bạn chưa có nội dung mới trong tuần. Hãy nạp đầu vào để hệ thống tạo quiz chính xác hơn.",
                    actionUrl = "/home/upload.html",
                    actionText = "Tải nội dung"
                });
            }

            if (totalAttemptsLast7Days < 3)
            {
                items.Add(new
                {
                    title = "Tăng nhịp quiz trong tuần",
                    detail = $"Hiện mới có {totalAttemptsLast7Days} lượt quiz trong 7 ngày. Mục tiêu tối thiểu là 3 lượt.",
                    actionUrl = "/home/quiz.html",
                    actionText = "Làm quiz ngay"
                });
            }

            items.Add(new
            {
                title = "Ưu tiên chủ đề yếu",
                detail = $"Chủ đề cần tập trung tuần này: {focusTopic}.",
                actionUrl = "/home/analytics.html",
                actionText = "Xem chủ đề yếu"
            });

            if (activeDaysCurrentWeek < WeeklyGoalSessions)
            {
                items.Add(new
                {
                    title = "Hoàn thành mục tiêu hoạt động tuần",
                    detail = $"Bạn đã hoạt động {activeDaysCurrentWeek}/{WeeklyGoalSessions} ngày. Mỗi ngày thêm 1 quiz ngắn là đủ đạt mục tiêu.",
                    actionUrl = "/home/history.html",
                    actionText = "Theo dõi lịch sử"
                });
            }

            return items;
        }

        private sealed class HistoryActivityCandidate
        {
            public DateTime At { get; set; }
            public string? Title { get; set; }
            public string? Kind { get; set; }
            public object? Result { get; set; }
            public bool IsQuiz { get; set; }
            public int? ContentId { get; set; }
            public int? QuizId { get; set; }
        }
    }
}
