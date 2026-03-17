using Microsoft.AspNetCore.Http;
using Web_Project.Models;

namespace Web_Project.Services.Content
{
    public interface ISummaryProcessingService
    {
        Task<SummarizeUploadResponse> SummarizeUploadAsync(
            IFormFile file,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken);

        Task<SummarizeUploadResponse> SummarizeTextAsync(
            string text,
            string? sourceHint,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken);

        Task<SummarizeUrlResponse> SummarizeFromUrlAsync(
            string url,
            int? userId,
            bool isGuest,
            CancellationToken cancellationToken);
    }
}
