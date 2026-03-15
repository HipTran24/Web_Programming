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
    }

    public sealed class UserProfileServiceResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public ProfileResponse? Response { get; init; }
    }
}
