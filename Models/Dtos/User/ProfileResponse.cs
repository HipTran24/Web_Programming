namespace Web_Project.Models.Dtos.User
{
    public class ProfileResponse
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsLocked { get; set; }

        public bool IsEmailVerified { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}