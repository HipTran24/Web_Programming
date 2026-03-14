using System.ComponentModel.DataAnnotations;

namespace Wed_Project.Models
{
    public class LoginRequest
    {
        [Required]
        [MaxLength(256)]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
