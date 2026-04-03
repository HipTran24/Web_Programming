using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;
using Web_Project.Models.Dtos.User;

namespace Web_Project.Services.Notifications
{
    public interface ISystemNotificationService
    {
        Task<UserSystemNotificationInboxResponse> GetUserInboxAsync(int userId, CancellationToken cancellationToken);

        Task<UserSystemNotificationUnreadResponse> GetUnreadSummaryAsync(int userId, CancellationToken cancellationToken);

        Task<SystemNotificationActionResult> MarkAsReadAsync(
            int userId,
            MarkSystemNotificationReadRequest request,
            CancellationToken cancellationToken);

        Task<SystemNotificationDispatchResult> SendCustomAsync(
            int adminUserId,
            AdminSendSystemNotificationRequest request,
            CancellationToken cancellationToken);

        Task<SystemNotificationHistoryResult> GetDispatchHistoryAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken);

        Task NotifyRegistrationCreatedAsync(
            User user,
            bool adminInitiated,
            CancellationToken cancellationToken);

        Task EnsureFirstLoginNotificationAsync(
            User user,
            CancellationToken cancellationToken);

        Task NotifyModerationDecisionAsync(
            int adminUserId,
            Web_Project.Models.Content content,
            string status,
            string reason,
            CancellationToken cancellationToken);

        Task NotifyLockStateChangedAsync(
            int adminUserId,
            User user,
            bool isLocked,
            string reason,
            CancellationToken cancellationToken);

        Task NotifyAccountDeletedAsync(
            int adminUserId,
            string email,
            string fullName,
            string username,
            string reason,
            CancellationToken cancellationToken);

        Task NotifySelfAccountDeletedAsync(
            User user,
            CancellationToken cancellationToken);
    }

    public sealed class SystemNotificationActionResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int UnreadCount { get; init; }
    }

    public sealed class SystemNotificationDispatchResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public int RecipientCount { get; init; }
    }

    public sealed class SystemNotificationHistoryResult
    {
        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalItems { get; init; }

        public int TotalPages { get; init; }

        public List<AdminSystemNotificationLogItemResponse> Items { get; init; } = [];
    }
}
