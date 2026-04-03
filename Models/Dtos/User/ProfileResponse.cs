namespace Web_Project.Models.Dtos.User
{
    public class ProfileResponse
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsLocked { get; set; }

        public bool IsEmailVerified { get; set; }

        public DateTime CreatedAt { get; set; }

        public int TotalUploads { get; set; }

        public int TotalQuizAttempts { get; set; }

        public double AverageQuizScore { get; set; }

        public int ActiveLearningDays { get; set; }
    }
}
