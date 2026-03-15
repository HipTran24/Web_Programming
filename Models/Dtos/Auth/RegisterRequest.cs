using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class RegisterRequest
    {
        [Required]
        [MaxLength(64)]
        [RegularExpression("^[a-zA-Z0-9._-]{3,64}$", ErrorMessage = "Username chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool AcceptTerms { get; set; }
    }
}
