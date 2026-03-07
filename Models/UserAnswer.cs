using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wed_Project.Models
{
    public class UserAnswer
    {
        [Key]
        public int AnswerId { get; set; }

        [ForeignKey(nameof(QuizAttempt))]
        public int AttemptId { get; set; }

        public int QuestionId { get; set; }

        public string SelectedAnswer { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public QuizAttempt QuizAttempt { get; set; } = null!;

        public Question Question { get; set; } = null!;
    }
}
