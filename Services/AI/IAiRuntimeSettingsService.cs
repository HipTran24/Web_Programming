using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;

namespace Web_Project.Services.AI
{
    public interface IAiRuntimeSettingsService
    {
        AiRuntimeSettingsSnapshot GetSnapshot();

        Task<AdminAiSystemSettingsResponse> GetAdminSettingsAsync(CancellationToken cancellationToken);

        Task<AdminAiSystemSettingsResponse> UpdateAdminSettingsAsync(
            AdminAiSystemSettingsUpdateRequest request,
            int adminUserId,
            CancellationToken cancellationToken);
    }

    public sealed class AiRuntimeSettingsSnapshot
    {
        public GeminiSettings Gemini { get; init; } = new();

        public GroqSettings Groq { get; init; } = new();

        public AiRoutingSettings Routing { get; init; } = new();

        public DateTime? UpdatedAt { get; init; }

        public int? UpdatedByUserId { get; init; }
    }
}
