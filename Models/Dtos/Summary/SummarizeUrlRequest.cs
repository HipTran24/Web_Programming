using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class SummarizeUrlRequest
    {
        [Required]
        [MaxLength(2048)]
        [Url]
        public string Url { get; set; } = string.Empty;
    }
}
