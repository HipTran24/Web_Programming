using Web_Project.Services.AI;

namespace Web_Project.Services.Content
{
    public sealed class GeminiContentSafetyService : IContentSafetyService
    {
        private readonly GeminiSummaryService _geminiSummaryService;
        private readonly KeywordContentSafetyService _keywordFallback;
        private readonly ILogger<GeminiContentSafetyService> _logger;

        public GeminiContentSafetyService(
            GeminiSummaryService geminiSummaryService,
            KeywordContentSafetyService keywordFallback,
            ILogger<GeminiContentSafetyService> logger)
        {
            _geminiSummaryService = geminiSummaryService;
            _keywordFallback = keywordFallback;
            _logger = logger;
        }

        public async Task<ContentSafetyReview> AnalyzeAsync(
            string extractedText,
            string summary,
            IReadOnlyCollection<string> keyPoints,
            string fileName,
            string? sourceUrl,
            CancellationToken cancellationToken)
        {
            var safeExtractedText = extractedText ?? string.Empty;
            var safeSummary = summary ?? string.Empty;
            var safeKeyPoints = keyPoints ?? Array.Empty<string>();
            var safeFileName = fileName ?? string.Empty;
            var corpus = string.Join(
                "\n\n",
                [
                    safeExtractedText,
                    safeSummary,
                    string.Join("\n", safeKeyPoints),
                    safeFileName,
                    sourceUrl ?? string.Empty
                ]).Trim();

            if (string.IsNullOrWhiteSpace(corpus))
            {
                return ContentSafetyReview.Safe;
            }

            try
            {
                var policyReview = await _geminiSummaryService.AnalyzePolicyAsync(
                    corpus,
                    safeFileName,
                    sourceUrl,
                    cancellationToken);

                if (!policyReview.IsViolation)
                {
                    return ContentSafetyReview.Safe;
                }

                var flags = policyReview.Flags
                    .Where(flag => !string.IsNullOrWhiteSpace(flag))
                    .Select(flag => flag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (flags.Count == 0)
                {
                    flags.Add("Gemini phát hiện nội dung có dấu hiệu phản động, kích động chính trị tiêu cực hoặc trái với chính sách hệ thống.");
                }

                return new ContentSafetyReview
                {
                    RequiresAdminReview = true,
                    BlocksSummarization = true,
                    IsPolicyViolation = true,
                    ModerationStatus = "Pending",
                    WarningTitle = "Nội dung bị chặn để chờ admin duyệt",
                    WarningMessage = "Gemini phát hiện nội dung có dấu hiệu vi phạm chính sách hệ thống nên hệ thống tạm dừng tóm tắt/phân tích. Nội dung đã được chuyển sang hàng chờ để admin xem xét.",
                    ModerationReason = ContentModerationPolicy.BuildPolicyViolationReason(flags, policyReview.Rationale),
                    ReviewSource = "Gemini",
                    Flags = flags
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Gemini policy review failed for file {FileName}. Falling back to keyword detector.",
                    safeFileName);

                return await _keywordFallback.AnalyzeAsync(
                    safeExtractedText,
                    safeSummary,
                    safeKeyPoints,
                    safeFileName,
                    sourceUrl,
                    cancellationToken);
            }
        }
    }
}
