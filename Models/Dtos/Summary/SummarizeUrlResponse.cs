namespace Web_Project.Models
{
    public class SummarizeUrlResponse
    {
        public int ContentId { get; set; }

        public string Url { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string InputType { get; set; } = string.Empty;

        public string DetectedMimeType { get; set; } = string.Empty;

        public int ExtractedTextLength { get; set; }

        public bool UsedVisionModel { get; set; }

        public bool UsedTranscription { get; set; }

        public string Summary { get; set; } = string.Empty;

        public List<string> KeyPoints { get; set; } = [];

        public string? Preview { get; set; }
    }
}
