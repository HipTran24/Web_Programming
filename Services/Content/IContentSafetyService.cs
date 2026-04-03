namespace Web_Project.Services.Content
{
    public interface IContentSafetyService
    {
        Task<ContentSafetyReview> AnalyzeAsync(
            string extractedText,
            string summary,
            IReadOnlyCollection<string> keyPoints,
            string fileName,
            string? sourceUrl,
            CancellationToken cancellationToken);
    }
}
