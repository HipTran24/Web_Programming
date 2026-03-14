using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class ContentModeration
    {
        [Key]
        public int ModerationId { get; set; }

        public int ContentId { get; set; }

        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = "Pending";

        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public int? ReviewedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public Content Content { get; set; } = null!;

        public User? ReviewedBy { get; set; }
    }
}
