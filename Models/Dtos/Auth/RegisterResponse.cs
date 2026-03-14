namespace Web_Project.Models
{
    public class RegisterResponse
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsEmailVerified { get; set; }

        public bool OtpDispatched { get; set; }

        public DateTime? OtpExpiresAt { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
