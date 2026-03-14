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
    }

    public sealed class AiSummaryResult
    {
        public string Summary { get; init; } = string.Empty;

        public List<string> KeyPoints { get; init; } = [];
    }
}
