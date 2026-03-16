namespace Web_Project.Models
{
    public class SubmitQuizResponse
    {
        public int QuizId { get; set; }

        public int AttemptId { get; set; }

        public int TotalQuestions { get; set; }

        public int CorrectCount { get; set; }

        public int WrongCount { get; set; }

        public double Score { get; set; }

        public List<WrongQuestionDetailResponse> WrongQuestions { get; set; } = [];
    }

    public class WrongQuestionDetailResponse
    {
        public int QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string SelectedAnswer { get; set; } = string.Empty;

        public string CorrectAnswer { get; set; } = string.Empty;

        public string Explanation { get; set; } = string.Empty;
    }
}
