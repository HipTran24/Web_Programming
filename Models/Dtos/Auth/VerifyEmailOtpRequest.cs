using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class VerifyEmailOtpRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "OTP phải gồm 6 chữ số.")]
        public string OtpCode { get; set; } = string.Empty;
    }
}
