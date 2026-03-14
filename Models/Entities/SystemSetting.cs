using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class SystemSetting
    {
        [Key]
        public int SettingId { get; set; }

        [Required]
        [MaxLength(128)]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        public string SettingValue { get; set; } = string.Empty;

        [MaxLength(512)]
        public string Description { get; set; } = string.Empty;

        public int? UpdatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public User? UpdatedBy { get; set; }
    }
}
