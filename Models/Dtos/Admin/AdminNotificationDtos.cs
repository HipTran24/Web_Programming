namespace Web_Project.Models.Dtos.Admin
{
    public class AdminSendSystemNotificationRequest
    {
        public string TargetScope { get; set; } = "all-users";

        public int? UserId { get; set; }

        public string UserEmail { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Category { get; set; } = "announcement";

        public string Severity { get; set; } = "info";

        public string ActionUrl { get; set; } = string.Empty;

        public bool SendEmail { get; set; } = true;
    }

    public class AdminSystemNotificationLogItemResponse
    {
        public string DispatchId { get; set; } = string.Empty;

        public int AdminUserId { get; set; }

        public string TargetScope { get; set; } = string.Empty;

        public string TargetLabel { get; set; } = string.Empty;

        public int RecipientCount { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public string ActionUrl { get; set; } = string.Empty;

        public bool EmailRequested { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
