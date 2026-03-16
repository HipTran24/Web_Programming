using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}