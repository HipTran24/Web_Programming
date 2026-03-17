namespace Web_Project.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

        public string TextModel { get; set; } = "gemini-2.0-flash";

        public string VisionModel { get; set; } = "gemini-2.0-flash";

        public string AudioModel { get; set; } = "gemini-2.0-flash";

        public int MaxInputCharacters { get; set; } = 16000;

        public int MaxQuizInputCharacters { get; set; } = 10000;

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxConcurrentRequests { get; set; } = 2;

        public int QueueWaitTimeoutSeconds { get; set; } = 8;

        public int MaxRetriesPerModel { get; set; } = 1;

        public bool EnableResponseCache { get; set; } = true;

        public int ResponseCacheMinutes { get; set; } = 30;

        public List<string> FallbackModels { get; set; } =
        [
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-flash-latest"
        ];
    }
}
