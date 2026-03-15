using Microsoft.AspNetCore.Http;
using Web_Project.Models;

namespace Web_Project.Services.Content
{
    public interface ISummaryProcessingService
    {
        Task<SummarizeUploadResponse> SummarizeUploadAsync(
            IFormFile file,
            CancellationToken cancellationToken);

        Task<SummarizeUploadResponse> SummarizeTextAsync(
            string text,
            string? sourceHint,
            CancellationToken cancellationToken);

        Task<SummarizeUrlResponse> SummarizeFromUrlAsync(
            string url,
            CancellationToken cancellationToken);
    }
}
