using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class GenerateQuizRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ContentId phải lớn hơn 0.")]
        public int ContentId { get; set; }

        [Range(0, 30, ErrorMessage = "Số lượng câu hỏi phải nằm trong khoảng 0-30.")]
        public int? TotalQuestions { get; set; }

        [MaxLength(20)]
        public string Difficulty { get; set; } = "medium";

        [MaxLength(20)]
        public string QuizType { get; set; } = "multiple-choice";

        [MaxLength(64)]
        public string VariationNonce { get; set; } = string.Empty;

        [MaxLength(128)]
        public string GuestToken { get; set; } = string.Empty;
    }
}
