namespace Web_Project.Models
{
    public class LoginResponse
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        public bool IsPremium { get; set; }

        public string SubscriptionTier { get; set; } = "Normal";

        public DateTime? PremiumStartedAt { get; set; }

        public DateTime? PremiumExpiresAt { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}
