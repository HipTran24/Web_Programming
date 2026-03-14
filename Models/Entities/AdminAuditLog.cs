using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class AdminAuditLog
    {
        [Key]
        public int AuditId { get; set; }

        public int AdminUserId { get; set; }

        [Required]
        [MaxLength(64)]
        public string ActionType { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string TargetType { get; set; } = string.Empty;

        [MaxLength(128)]
        public string TargetId { get; set; } = string.Empty;

        public string DetailJson { get; set; } = string.Empty;

        [MaxLength(64)]
        public string IpAddress { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public User AdminUser { get; set; } = null!;
    }
}
