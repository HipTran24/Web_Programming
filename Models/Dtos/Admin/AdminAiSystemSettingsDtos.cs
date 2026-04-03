namespace Web_Project.Models.Dtos.Admin
{
    public class AdminAiSystemSettingsResponse
    {
        public AdminAiRoutingSettingsDto Routing { get; set; } = new();

        public AdminGeminiSettingsDto Gemini { get; set; } = new();

        public AdminGroqSettingsDto Groq { get; set; } = new();

        public DateTime? UpdatedAt { get; set; }

        public int? UpdatedByUserId { get; set; }
    }

    public class AdminAiSystemSettingsUpdateRequest
    {
        public AdminAiRoutingSettingsDto Routing { get; set; } = new();

        public AdminGeminiSettingsUpdateDto Gemini { get; set; } = new();

        public AdminGroqSettingsUpdateDto Groq { get; set; } = new();
    }

    public class AdminAiRoutingSettingsDto
    {
        public string PrimaryTextProvider { get; set; } = "gemini";

        public string PrimaryVisionProvider { get; set; } = "gemini";

        public bool EnforceDailyTokenBudget { get; set; }

        public int ApproxCharsPerToken { get; set; } = 4;

        public int TextOutputTokenBudget { get; set; } = 1500;

        public int QuizOutputTokenBudget { get; set; } = 2200;

        public int ImageOutputTokenBudget { get; set; } = 900;

        public int GeminiDailyTokenBudget { get; set; }

        public int GroqDailyTokenBudget { get; set; }

        public int MinReservedTokensPerProvider { get; set; }

        public bool EnableProviderHealthSwitch { get; set; } = true;

        public int SlowRequestThresholdMs { get; set; } = 9000;

        public int SlowRequestStreakThreshold { get; set; } = 1;

        public int ConsecutiveFailureThreshold { get; set; } = 2;

        public int ProviderCooldownSeconds { get; set; } = 45;

        public bool PreferFastestHealthyProvider { get; set; } = true;

        public int ProviderExecutionTimeoutMs { get; set; } = 12000;
    }

    public class AdminGeminiSettingsDto
    {
        public bool HasApiKey { get; set; }

        public string BaseUrl { get; set; } = string.Empty;

        public string TextModel { get; set; } = string.Empty;

        public string VisionModel { get; set; } = string.Empty;

        public int MaxInputCharacters { get; set; }

        public int MaxQuizInputCharacters { get; set; }

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxRetriesPerModel { get; set; } = 1;

        public List<string> FallbackModels { get; set; } = [];
    }

    public class AdminGeminiSettingsUpdateDto
    {
        public string ApiKey { get; set; } = string.Empty;

        public bool ClearApiKey { get; set; }

        public string BaseUrl { get; set; } = string.Empty;

        public string TextModel { get; set; } = string.Empty;

        public string VisionModel { get; set; } = string.Empty;

        public int MaxInputCharacters { get; set; }

        public int MaxQuizInputCharacters { get; set; }

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxRetriesPerModel { get; set; } = 1;

        public List<string> FallbackModels { get; set; } = [];
    }

    public class AdminGroqSettingsDto
    {
        public bool HasApiKey { get; set; }

        public string BaseUrl { get; set; } = string.Empty;

        public string TextModel { get; set; } = string.Empty;

        public string VisionModel { get; set; } = string.Empty;

        public string AudioModel { get; set; } = string.Empty;

        public int MaxInputCharacters { get; set; }

        public int MaxQuizInputCharacters { get; set; }

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxConcurrentRequests { get; set; } = 2;

        public int QueueWaitTimeoutSeconds { get; set; } = 8;

        public int MaxRetriesPerModel { get; set; } = 1;

        public bool EnableResponseCache { get; set; } = true;

        public int ResponseCacheDays { get; set; } = 7;

        public List<string> FallbackModels { get; set; } = [];
    }

    public class AdminGroqSettingsUpdateDto
    {
        public string ApiKey { get; set; } = string.Empty;

        public bool ClearApiKey { get; set; }

        public string BaseUrl { get; set; } = string.Empty;

        public string TextModel { get; set; } = string.Empty;

        public string VisionModel { get; set; } = string.Empty;

        public string AudioModel { get; set; } = string.Empty;

        public int MaxInputCharacters { get; set; }

        public int MaxQuizInputCharacters { get; set; }

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxConcurrentRequests { get; set; } = 2;

        public int QueueWaitTimeoutSeconds { get; set; } = 8;

        public int MaxRetriesPerModel { get; set; } = 1;

        public bool EnableResponseCache { get; set; } = true;

        public int ResponseCacheDays { get; set; } = 7;

        public List<string> FallbackModels { get; set; } = [];
    }
}
