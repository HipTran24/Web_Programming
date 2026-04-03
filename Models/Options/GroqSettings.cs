namespace Web_Project.Models
{
    public class GroqSettings
    {
        public string GroqApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.groq.com/openai";

        public string TextModel { get; set; } = "llama-3.3-70b-versatile";

        public string VisionModel { get; set; } = "llama-3.2-90b-vision-preview";

        public string AudioModel { get; set; } = "whisper-large-v3-turbo";

        public int MaxInputCharacters { get; set; } = 0;

        public int MaxQuizInputCharacters { get; set; } = 0;

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxConcurrentRequests { get; set; } = 2;

        public int QueueWaitTimeoutSeconds { get; set; } = 8;

        public int MaxRetriesPerModel { get; set; } = 1;

        public bool EnableResponseCache { get; set; } = true;

        public int ResponseCacheDays { get; set; } = 7;

        public int ResponseCacheMinutes { get; set; }

        public List<string> FallbackModels { get; set; } =
        [
            "llama-3.1-8b-instant",
            "llama3-70b-8192"
        ];
    }
}
