using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class CreateContentFromUrlRequest
    {
        [Required]
        [MaxLength(2048)]
        [Url]
        public string Url { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public bool IsGuest { get; set; }
    }
}
