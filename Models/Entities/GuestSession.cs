using System.ComponentModel.DataAnnotations;

namespace Web_Project.Models
{
    public class GuestSession
    {
        [Key]
        public int GuestSessionId { get; set; }

        [Required]
        [MaxLength(128)]
        public string GuestToken { get; set; } = string.Empty;

        [MaxLength(256)]
        public string FingerprintHash { get; set; } = string.Empty;

        [MaxLength(64)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(512)]
        public string UserAgent { get; set; } = string.Empty;

        public DateTime FirstSeenAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public DateTime? TrialUsedAt { get; set; }

        public bool IsBlocked { get; set; }

        public ICollection<DailyUsageCounter> DailyUsageCounters { get; set; } = new List<DailyUsageCounter>();
    }
}
