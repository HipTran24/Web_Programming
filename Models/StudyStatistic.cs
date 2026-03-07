using System.ComponentModel.DataAnnotations;

namespace Wed_Project.Models
{
    public class StudyStatistic
    {
        [Key]
        public int StatId { get; set; }

        public int UserId { get; set; }

        public double AverageScore { get; set; }

        public int TotalAttempts { get; set; }

        public string WeakTopic { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; }

        public User User { get; set; } = null!;
    }
}
