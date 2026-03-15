using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class Content
    {
        public int ContentId { get; set; }

        public int? UserId { get; set; }

        public bool IsGuest { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FileType { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        [MaxLength(32)]
        public string SourceType { get; set; } = "FileUpload";

        [MaxLength(2048)]
        public string? SourceUrl { get; set; }

        [MaxLength(32)]
        public string? FetchStatus { get; set; }

        [MaxLength(2000)]
        public string? FetchError { get; set; }

        public string ExtractedText { get; set; } = string.Empty;

        public string AI_DetectedSubject { get; set; } = string.Empty;

        public string AI_DetectedGrade { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }

        public AIProcess? AIProcess { get; set; }

        public ContentModeration? ContentModeration { get; set; }

        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
