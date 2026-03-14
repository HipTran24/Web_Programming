using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class DailyUsageCounter
    {
        [Key]
        public int CounterId { get; set; }

        public DateTime UsageDate { get; set; }

        public int? UserId { get; set; }

        public int? GuestSessionId { get; set; }

        public int UploadCount { get; set; }

        public int AIProcessCount { get; set; }

        public int QuizGenerationCount { get; set; }

        public double TotalProcessingTime { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }

        public GuestSession? GuestSession { get; set; }
    }
}
