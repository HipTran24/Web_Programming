namespace Web_Project.Services.AI
{
    public sealed class AiPolicyReviewResult
    {
        public bool IsViolation { get; init; }

        public string RiskLevel { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Rationale { get; init; } = string.Empty;

        public List<string> Flags { get; init; } = [];
    }
}
