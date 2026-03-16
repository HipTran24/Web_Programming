namespace Web_Project.Models
{
    public class GenerateQuizResponse
    {
        public int QuizId { get; set; }

        public int ContentId { get; set; }

        public int TotalQuestions { get; set; }

        public string Difficulty { get; set; } = string.Empty;

        public string QuizType { get; set; } = string.Empty;

        public bool IsGuest { get; set; }

        public DateTime CreatedAt { get; set; }

        public string GuestToken { get; set; } = string.Empty;

        public List<GeneratedQuestionResponse> Questions { get; set; } = [];
    }

    public class GeneratedQuestionResponse
    {
        public int QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string OptionA { get; set; } = string.Empty;

        public string OptionB { get; set; } = string.Empty;

        public string OptionC { get; set; } = string.Empty;

        public string OptionD { get; set; } = string.Empty;

        public string? CorrectAnswer { get; set; }

        public string? Explanation { get; set; }
    }
}
