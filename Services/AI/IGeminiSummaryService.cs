namespace Web_Project.Services.AI
{
    public interface IGeminiSummaryService
    {
        Task<AiSummaryResult> SummarizeTextAsync(
            string text,
            string sourceHint,
            CancellationToken cancellationToken);

        Task<AiSummaryResult> SummarizeImageAsync(
            byte[] imageBytes,
            string mimeType,
            string fileName,
            CancellationToken cancellationToken);

        Task<string> TranscribeAudioAsync(
            string audioFilePath,
            CancellationToken cancellationToken);

        Task<AiQuizResult> GenerateQuizAsync(
            string sourceText,
            int totalQuestions,
            string difficulty,
            string quizType,
            CancellationToken cancellationToken);
    }

    public sealed class AiSummaryResult
    {
        public string Summary { get; init; } = string.Empty;

        public List<string> KeyPoints { get; init; } = [];
    }

    public sealed class AiQuizResult
    {
        public List<AiQuizQuestion> Questions { get; init; } = [];
    }

    public sealed class AiQuizQuestion
    {
        public string QuestionText { get; init; } = string.Empty;

        public string OptionA { get; init; } = string.Empty;

        public string OptionB { get; init; } = string.Empty;

        public string OptionC { get; init; } = string.Empty;

        public string OptionD { get; init; } = string.Empty;

        public string CorrectAnswer { get; init; } = "A";

        public string Explanation { get; init; } = string.Empty;
    }
}
