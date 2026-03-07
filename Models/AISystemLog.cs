using System.ComponentModel.DataAnnotations;

namespace Wed_Project.Models
{
    public class AISystemLog
    {
        [Key]
        public int LogId { get; set; }

        public string ActionType { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public bool IsGuest { get; set; }

        public double ProcessingTime { get; set; }

        public bool IsError { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }
    }
}
