using Web_Project.Models.Dtos.User;

namespace Web_Project.Services.Users
{
    public interface IUserService
    {
        Task<UserProfileServiceResult> GetProfileAsync(int userId, CancellationToken cancellationToken);

        Task<UserProfileServiceResult> UpdateProfileAsync(
            int userId,
            UpdateProfileRequest request,
            CancellationToken cancellationToken);

        Task<UserActionServiceResult> ChangePasswordAsync(
            int userId,
            ChangePasswordRequest request,
            CancellationToken cancellationToken);

        Task<UserNotificationSettingsServiceResult> GetNotificationSettingsAsync(
            int userId,
            CancellationToken cancellationToken);

        Task<UserNotificationSettingsServiceResult> UpdateNotificationSettingsAsync(
            int userId,
            NotificationSettingsRequest request,
            CancellationToken cancellationToken);

        Task<UserActionServiceResult> DeleteAccountAsync(
            int userId,
            CancellationToken cancellationToken);
    }

    public sealed class UserProfileServiceResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public ProfileResponse? Response { get; init; }
    }

    public sealed class UserActionServiceResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public sealed class UserNotificationSettingsServiceResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public NotificationSettingsResponse Response { get; init; } = new();
    }
}
