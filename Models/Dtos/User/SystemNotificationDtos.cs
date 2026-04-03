namespace Web_Project.Models.Dtos.User
{
    public class UserSystemNotificationItemResponse
    {
        public string NotificationId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public string ActionUrl { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }
    }

    public class UserSystemNotificationInboxResponse
    {
        public int TotalItems { get; set; }

        public int UnreadCount { get; set; }

        public List<UserSystemNotificationItemResponse> Items { get; set; } = [];
    }

    public class UserSystemNotificationUnreadResponse
    {
        public int UnreadCount { get; set; }

        public bool HasUnread => UnreadCount > 0;
    }

    public class MarkSystemNotificationReadRequest
    {
        public string NotificationId { get; set; } = string.Empty;

        public bool MarkAll { get; set; }
    }
}
