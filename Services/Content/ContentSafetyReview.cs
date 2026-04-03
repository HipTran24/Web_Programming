namespace Web_Project.Services.Content
{
    public sealed class ContentSafetyReview
    {
        public static ContentSafetyReview Safe { get; } = new();

        public bool RequiresAdminReview { get; init; }

        public bool BlocksSummarization { get; init; }

        public bool IsPolicyViolation { get; init; }

        public string ModerationStatus { get; init; } = string.Empty;

        public string WarningTitle { get; init; } = string.Empty;

        public string WarningMessage { get; init; } = string.Empty;

        public string ModerationReason { get; init; } = string.Empty;

        public string ReviewSource { get; init; } = string.Empty;

        public List<string> Flags { get; init; } = [];
    }
}
