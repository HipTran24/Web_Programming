using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wed_Project.Models
{
    public class QuizAttempt
    {
        [Key]
        public int AttemptId { get; set; }

        public int QuizId { get; set; }

        public int? UserId { get; set; }

        public double Score { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime SubmittedAt { get; set; }

        public Quiz Quiz { get; set; } = null!;

        public User? User { get; set; }

        public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    }
}
