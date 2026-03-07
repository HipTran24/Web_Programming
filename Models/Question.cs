using System.Collections.Generic;

namespace Wed_Project.Models
{
    public class Question
    {
        public int QuestionId { get; set; }

        public int QuizId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string OptionA { get; set; } = string.Empty;

        public string OptionB { get; set; } = string.Empty;

        public string OptionC { get; set; } = string.Empty;

        public string OptionD { get; set; } = string.Empty;

        public string CorrectAnswer { get; set; } = string.Empty;

        public string Explanation { get; set; } = string.Empty;

        public Quiz Quiz { get; set; } = null!;

        public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    }
}
