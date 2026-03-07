using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wed_Project.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; }

        public bool IsLocked { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey(nameof(RoleId))]
        public Role Role { get; set; } = null!;

        public ICollection<Content> Contents { get; set; } = new List<Content>();

        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();

        public StudyStatistic? StudyStatistic { get; set; }
    }
}
