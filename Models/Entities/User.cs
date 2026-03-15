using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Project.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(128)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; }

        public bool IsLocked { get; set; }

        public bool IsEmailVerified { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role Role { get; set; } = null!;

        public ICollection<Content> Contents { get; set; } = new List<Content>();

        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

        public StudyStatistic? StudyStatistic { get; set; }

        public ICollection<DailyUsageCounter> DailyUsageCounters { get; set; } = new List<DailyUsageCounter>();

        public ICollection<ContentModeration> ReviewedContentModerations { get; set; } = new List<ContentModeration>();

        public ICollection<AdminAuditLog> AdminAuditLogs { get; set; } = new List<AdminAuditLog>();

        public ICollection<SystemSetting> UpdatedSystemSettings { get; set; } = new List<SystemSetting>();
    }
}
