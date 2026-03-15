using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class ResendEmailOtpRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }
}
