namespace Web_Project.Models
{
    public class ContentFromUrlResponse
    {
        public int ContentId { get; set; }

        public string SourceType { get; set; } = string.Empty;

        public string FetchStatus { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
