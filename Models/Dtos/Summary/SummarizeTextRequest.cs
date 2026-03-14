using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class SummarizeTextRequest
    {
        [Required]
        public string Text { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? SourceHint { get; set; }
    }
}
