using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class SubmitQuizRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "QuizId phải lớn hơn 0.")]
        public int QuizId { get; set; }

        public List<SubmitQuizAnswerRequest> Answers { get; set; } = [];

        [MaxLength(128)]
        public string GuestToken { get; set; } = string.Empty;
    }

    public class SubmitQuizAnswerRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "QuestionId không hợp lệ.")]
        public int QuestionId { get; set; }

        [MaxLength(1)]
        public string SelectedAnswer { get; set; } = string.Empty;
    }
}
