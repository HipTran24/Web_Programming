using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models.Dtos.User
{
    public class ChangePasswordRequest
    {
        [Required]
        [MaxLength(128)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
