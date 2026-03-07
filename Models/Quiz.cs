using System.Collections.Generic;

namespace Wed_Project.Models
{
    public class Quiz
    {
        public int QuizId { get; set; }

        public int ContentId { get; set; }

        public int? UserId { get; set; }

        public bool IsGuest { get; set; }

        public int TotalQuestions { get; set; }

        public string Difficulty { get; set; } = string.Empty;

        public string QuizType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public Content Content { get; set; } = null!;

        public User? User { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();

        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    }
}
