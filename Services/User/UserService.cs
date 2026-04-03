using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Web_Project.Models;
using Web_Project.Models.Dtos.User;
using Web_Project.Security;
using Web_Project.Services.Notifications;

namespace Web_Project.Services.Users
{
    public class UserService : IUserService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private const int MaxPhoneLength = 32;
        private const int MaxBioLength = 1000;
        private const int MaxAvatarUrlLength = 3000000;

        private readonly AppDbContext _dbContext;
        private readonly ISystemNotificationService? _systemNotificationService;

        public UserService(
            AppDbContext dbContext,
            ISystemNotificationService? systemNotificationService = null)
        {
            _dbContext = dbContext;
            _systemNotificationService = systemNotificationService;
        }

        public async Task<UserProfileServiceResult> GetProfileAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    x.RoleId,
                    x.IsLocked,
                    x.IsEmailVerified,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            var roleName = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.RoleId == user.RoleId)
                .Select(x => x.RoleName)
                .FirstOrDefaultAsync(cancellationToken) ?? "User";

            var meta = await LoadProfileMetaAsync(user.UserId, cancellationToken);

            var stats = await GetProfileStatsAsync(userId, cancellationToken);

            return new UserProfileServiceResult
            {
                Success = true,
                Response = new ProfileResponse
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = meta.Phone,
                    Bio = meta.Bio,
                    AvatarUrl = meta.AvatarUrl,
                    Role = roleName,
                    IsLocked = user.IsLocked,
                    IsEmailVerified = user.IsEmailVerified,
                    CreatedAt = user.CreatedAt,
                    TotalUploads = stats.TotalUploads,
                    TotalQuizAttempts = stats.TotalQuizAttempts,
                    AverageQuizScore = stats.AverageQuizScore,
                    ActiveLearningDays = stats.ActiveLearningDays
                }
            };
        }

        public async Task<UserProfileServiceResult> UpdateProfileAsync(
            int userId,
            UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            var normalizedEmail = NormalizeEmail(request.Email);
            var fullName = NormalizeText(request.FullName);
            var phone = TrimToMax(request.Phone, MaxPhoneLength);
            var bio = TrimToMax(request.Bio, MaxBioLength);
            var avatarUrl = NormalizeAvatarUrl(request.AvatarUrl);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Họ tên không được để trống."
                };
            }

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Email không được để trống."
                };
            }

            var emailInUse = await _dbContext.Users
                .AnyAsync(x => x.UserId != userId && x.Email == normalizedEmail, cancellationToken);

            if (emailInUse)
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Email đã được sử dụng bởi tài khoản khác."
                };
            }

            user.FullName = fullName;
            user.Email = normalizedEmail;

            await UpsertProfileMetaAsync(userId, phone, bio, avatarUrl, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var role = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.RoleId == user.RoleId)
                .Select(x => x.RoleName)
                .FirstOrDefaultAsync(cancellationToken) ?? "User";

            var stats = await GetProfileStatsAsync(userId, cancellationToken);

            return new UserProfileServiceResult
            {
                Success = true,
                Message = "Cập nhật hồ sơ thành công.",
                Response = new ProfileResponse
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = phone,
                    Bio = bio,
                    AvatarUrl = avatarUrl,
                    Role = role,
                    IsLocked = user.IsLocked,
                    IsEmailVerified = user.IsEmailVerified,
                    CreatedAt = user.CreatedAt,
                    TotalUploads = stats.TotalUploads,
                    TotalQuizAttempts = stats.TotalQuizAttempts,
                    AverageQuizScore = stats.AverageQuizScore,
                    ActiveLearningDays = stats.ActiveLearningDays
                }
            };
        }

        public async Task<UserActionServiceResult> ChangePasswordAsync(
            int userId,
            ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return new UserActionServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            if (!PasswordHashUtility.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                return new UserActionServiceResult
                {
                    Success = false,
                    Message = "Mật khẩu hiện tại không chính xác."
                };
            }

            if (request.CurrentPassword == request.NewPassword)
            {
                return new UserActionServiceResult
                {
                    Success = false,
                    Message = "Mật khẩu mới phải khác mật khẩu hiện tại."
                };
            }

            if (!HasStrongPassword(request.NewPassword))
            {
                return new UserActionServiceResult
                {
                    Success = false,
                    Message = "Mật khẩu mới cần tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số."
                };
            }

            user.PasswordHash = PasswordHashUtility.HashPassword(request.NewPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UserActionServiceResult
            {
                Success = true,
                Message = "Đổi mật khẩu thành công."
            };
        }

        public async Task<UserNotificationSettingsServiceResult> GetNotificationSettingsAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var userExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId, cancellationToken);

            if (!userExists)
            {
                return new UserNotificationSettingsServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            var settingKey = BuildNotificationSettingKey(userId);
            var setting = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == settingKey)
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            return new UserNotificationSettingsServiceResult
            {
                Success = true,
                Response = DeserializeNotificationSettings(setting)
            };
        }

        public async Task<UserNotificationSettingsServiceResult> UpdateNotificationSettingsAsync(
            int userId,
            NotificationSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var userExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId, cancellationToken);

            if (!userExists)
            {
                return new UserNotificationSettingsServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            var settingKey = BuildNotificationSettingKey(userId);
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == settingKey, cancellationToken);

            var response = new NotificationSettingsResponse
            {
                NotifyReviewReminder = request.NotifyReviewReminder,
                NotifyQuizResult = request.NotifyQuizResult,
                NotifyProductNews = request.NotifyProductNews
            };

            var serialized = JsonSerializer.Serialize(response, JsonOptions);
            var now = DateTime.UtcNow;

            if (setting is null)
            {
                setting = new SystemSetting
                {
                    SettingKey = settingKey,
                    SettingValue = serialized,
                    Description = "User notification preferences",
                    UpdatedByUserId = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbContext.SystemSettings.Add(setting);
            }
            else
            {
                setting.SettingValue = serialized;
                setting.Description = "User notification preferences";
                setting.UpdatedByUserId = userId;
                setting.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UserNotificationSettingsServiceResult
            {
                Success = true,
                Message = "Cập nhật cài đặt thông báo thành công.",
                Response = response
            };
        }

        public async Task<UserActionServiceResult> DeleteAccountAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return new UserActionServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            user.Status = false;
            user.IsLocked = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_systemNotificationService is not null)
            {
                await _systemNotificationService.NotifySelfAccountDeletedAsync(user, cancellationToken);
            }

            return new UserActionServiceResult
            {
                Success = true,
                Message = "Tài khoản đã được ẩn thành công."
            };
        }

        private static bool HasStrongPassword(string? password)
        {
            var value = password ?? string.Empty;
            if (value.Length < 8)
            {
                return false;
            }

            var hasUpper = value.Any(char.IsUpper);
            var hasLower = value.Any(char.IsLower);
            var hasDigit = value.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }

        private static string BuildNotificationSettingKey(int userId)
        {
            return $"user:{userId}:notifications";
        }

        private static string BuildUserSettingPrefix(int userId)
        {
            return $"user:{userId}:";
        }

        private static string BuildProfileMetaSettingKey(int userId)
        {
            return $"user:{userId}:profile-meta";
        }

        private static NotificationSettingsResponse DeserializeNotificationSettings(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new NotificationSettingsResponse();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<NotificationSettingsResponse>(raw, JsonOptions);
                return parsed ?? new NotificationSettingsResponse();
            }
            catch
            {
                return new NotificationSettingsResponse();
            }
        }

        private async Task<UserProfileMeta> LoadProfileMetaAsync(int userId, CancellationToken cancellationToken)
        {
            var key = BuildProfileMetaSettingKey(userId);
            var raw = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == key)
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new UserProfileMeta();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<UserProfileMeta>(raw, JsonOptions);
                return parsed ?? new UserProfileMeta();
            }
            catch
            {
                return new UserProfileMeta();
            }
        }

        private async Task UpsertProfileMetaAsync(
            int userId,
            string phone,
            string bio,
            string avatarUrl,
            CancellationToken cancellationToken)
        {
            var key = BuildProfileMetaSettingKey(userId);
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);

            var payload = JsonSerializer.Serialize(new UserProfileMeta
            {
                Phone = phone,
                Bio = bio,
                AvatarUrl = avatarUrl
            }, JsonOptions);

            var now = DateTime.UtcNow;
            if (setting is null)
            {
                _dbContext.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = payload,
                    Description = "User profile meta",
                    UpdatedByUserId = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                return;
            }

            setting.SettingValue = payload;
            setting.Description = "User profile meta";
            setting.UpdatedByUserId = userId;
            setting.UpdatedAt = now;
        }

        private static string TrimToMax(string? value, int maxLength)
        {
            var text = NormalizeText(value);
            return text.Length <= maxLength ? text : text[..maxLength];
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeEmail(string? value)
        {
            return NormalizeText(value).ToLowerInvariant();
        }

        private static string NormalizeAvatarUrl(string? value)
        {
            var normalized = NormalizeText(value);
            return normalized.Length <= MaxAvatarUrlLength
                ? normalized
                : normalized[..MaxAvatarUrlLength];
        }

        private async Task<ProfileStatsSnapshot> GetProfileStatsAsync(int userId, CancellationToken cancellationToken)
        {
            var totalUploads = await _dbContext.Contents
                .CountAsync(x => x.UserId == userId && !x.IsGuest, cancellationToken);

            var totalQuizAttempts = await _dbContext.QuizAttempts
                .CountAsync(x => x.UserId == userId, cancellationToken);

            var averageQuizScoreRaw = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId)
                .Select(x => (double?)x.Score)
                .AverageAsync(cancellationToken) ?? 0d;

            var activeLearningDays = await _dbContext.QuizAttempts
                .Where(x => x.UserId == userId)
                .Select(x => x.SubmittedAt.Date)
                .Distinct()
                .CountAsync(cancellationToken);

            return new ProfileStatsSnapshot
            {
                TotalUploads = totalUploads,
                TotalQuizAttempts = totalQuizAttempts,
                AverageQuizScore = Math.Round(averageQuizScoreRaw, 2, MidpointRounding.AwayFromZero),
                ActiveLearningDays = activeLearningDays
            };
        }

        private sealed class ProfileStatsSnapshot
        {
            public int TotalUploads { get; set; }

            public int TotalQuizAttempts { get; set; }

            public double AverageQuizScore { get; set; }

            public int ActiveLearningDays { get; set; }
        }

        private sealed class UserProfileMeta
        {
            public string Phone { get; set; } = string.Empty;

            public string Bio { get; set; } = string.Empty;

            public string AvatarUrl { get; set; } = string.Empty;
        }
    }
}
