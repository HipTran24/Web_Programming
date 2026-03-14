using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class EmailVerificationOtp
    {
        [Key]
        public int OtpId { get; set; }

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Purpose { get; set; } = string.Empty;

        public int? UserId { get; set; }

        [Required]
        [MaxLength(128)]
        public string OtpHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string OtpSalt { get; set; } = string.Empty;

        public int AttemptCount { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime? UsedAt { get; set; }

        [MaxLength(64)]
        public string RequestedIp { get; set; } = string.Empty;

        public User? User { get; set; }
    }
}
