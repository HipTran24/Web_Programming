using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models.Dtos.User
{
    public class UpdateProfileRequest
    {
        [Required]
        [MaxLength(128)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(32)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Bio { get; set; } = string.Empty;

        [MaxLength(3000000)]
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
