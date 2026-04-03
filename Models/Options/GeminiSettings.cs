namespace Web_Project.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

        public string TextModel { get; set; } = "gemini-2.0-flash";

        public string VisionModel { get; set; } = "gemini-2.0-flash";

        public int MaxInputCharacters { get; set; } = 0;

        public int MaxQuizInputCharacters { get; set; } = 0;

        public int RequestTimeoutSeconds { get; set; } = 20;

        public int MaxModelCandidates { get; set; } = 2;

        public int MaxRetriesPerModel { get; set; } = 1;

        public List<string> FallbackModels { get; set; } =
        [
            "gemini-1.5-flash"
        ];
    }
}
