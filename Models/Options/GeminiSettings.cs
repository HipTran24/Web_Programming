namespace Web_Project.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

        public string TextModel { get; set; } = "gemini-2.0-flash";

        public string VisionModel { get; set; } = "gemini-2.0-flash";

        public string AudioModel { get; set; } = "gemini-2.0-flash";

        public int MaxInputCharacters { get; set; } = 24000;

        public List<string> FallbackModels { get; set; } =
        [
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-flash-latest"
        ];
    }
}
