using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;
using Web_Project.Models.Dtos.User;
using Web_Project.Services.Content;
using Web_Project.Services.Email;

namespace Web_Project.Services.Notifications
{
    public class SystemNotificationService : ISystemNotificationService
    {
        private const int MaxInboxItems = 60;
        private const int MaxDispatchLogItems = 120;
        private const string DispatchHistoryKey = "system:notification:dispatch-history";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly AppDbContext _dbContext;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<SystemNotificationService> _logger;

        public SystemNotificationService(
            AppDbContext dbContext,
            IEmailSender emailSender,
            ILogger<SystemNotificationService> logger)
        {
            _dbContext = dbContext;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<UserSystemNotificationInboxResponse> GetUserInboxAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var inbox = await LoadUserInboxAsync(userId, cancellationToken);
            return BuildInboxResponse(inbox);
        }

        public async Task<UserSystemNotificationUnreadResponse> GetUnreadSummaryAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var inbox = await LoadUserInboxAsync(userId, cancellationToken);
            return new UserSystemNotificationUnreadResponse
            {
                UnreadCount = inbox.Count(x => !x.IsRead)
            };
        }

        public async Task<SystemNotificationActionResult> MarkAsReadAsync(
            int userId,
            MarkSystemNotificationReadRequest request,
            CancellationToken cancellationToken)
        {
            var setting = await GetUserInboxSettingAsync(userId, cancellationToken);
            var inbox = DeserializeList<StoredSystemNotification>(setting?.SettingValue);
            var now = DateTime.UtcNow;

            if (request.MarkAll)
            {
                foreach (var item in inbox.Where(x => !x.IsRead))
                {
                    item.IsRead = true;
                    item.ReadAt = now;
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.NotificationId))
            {
                var match = inbox.FirstOrDefault(x => x.NotificationId == request.NotificationId);
                if (match is not null && !match.IsRead)
                {
                    match.IsRead = true;
                    match.ReadAt = now;
                }
            }

            await UpsertSettingAsync(
                settingKey: BuildUserInboxKey(userId),
                setting: setting,
                settingValue: JsonSerializer.Serialize(inbox, JsonOptions),
                description: "User system notification inbox",
                updatedByUserId: userId,
                cancellationToken: cancellationToken);

            return new SystemNotificationActionResult
            {
                Success = true,
                Message = request.MarkAll ? "Đã đánh dấu toàn bộ thông báo là đã đọc." : "Đã cập nhật trạng thái thông báo.",
                UnreadCount = inbox.Count(x => !x.IsRead)
            };
        }

        public async Task<SystemNotificationDispatchResult> SendCustomAsync(
            int adminUserId,
            AdminSendSystemNotificationRequest request,
            CancellationToken cancellationToken)
        {
            var scope = NormalizeTargetScope(request.TargetScope);
            var title = TrimTo(request.Title, 140);
            var message = TrimTo(request.Message, 1800);
            var category = NormalizeCategory(request.Category);
            var severity = NormalizeSeverity(request.Severity);
            var actionUrl = NormalizeActionUrl(request.ActionUrl);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                return new SystemNotificationDispatchResult
                {
                    Success = false,
                    Message = "Tiêu đề và nội dung thông báo không được để trống."
                };
            }

            var recipients = await ResolveRecipientsAsync(scope, request.UserId, request.UserEmail, cancellationToken);
            if (recipients.Count == 0)
            {
                return new SystemNotificationDispatchResult
                {
                    Success = false,
                    Message = "Không tìm thấy người nhận hợp lệ."
                };
            }

            var delivered = 0;
            foreach (var recipient in recipients)
            {
                var shouldSendEmail = request.SendEmail && CanSendEmailForCategory(recipient.Settings, category);
                await PushNotificationAsync(
                    recipient.User,
                    title,
                    message,
                    category,
                    severity,
                    source: "AdminCustom",
                    actionUrl,
                    shouldSendEmail,
                    cancellationToken);
                delivered += 1;
            }

            await AppendDispatchLogAsync(new StoredDispatchLog
            {
                DispatchId = Guid.NewGuid().ToString("N"),
                AdminUserId = adminUserId,
                TargetScope = scope,
                TargetLabel = scope == "user"
                    ? recipients[0].User.Email
                    : "Toàn bộ người dùng",
                RecipientCount = delivered,
                Title = title,
                Message = message,
                Category = category,
                Severity = severity,
                ActionUrl = actionUrl,
                EmailRequested = request.SendEmail,
                CreatedAt = DateTime.UtcNow
            }, adminUserId, cancellationToken);

            return new SystemNotificationDispatchResult
            {
                Success = true,
                Message = delivered == 1 ? "Đã gửi thông báo tới người dùng." : $"Đã gửi thông báo tới {delivered} người dùng.",
                RecipientCount = delivered
            };
        }

        public async Task<SystemNotificationHistoryResult> GetDispatchHistoryAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var logs = await LoadDispatchLogsAsync(cancellationToken);
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 50);
            var totalItems = logs.Count;
            var items = logs
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(MapDispatchLog)
                .ToList();

            return new SystemNotificationHistoryResult
            {
                Page = safePage,
                PageSize = safePageSize,
                TotalItems = totalItems,
                TotalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                Items = items
            };
        }

        public async Task NotifyRegistrationCreatedAsync(
            User user,
            bool adminInitiated,
            CancellationToken cancellationToken)
        {
            var title = adminInitiated
                ? "Tài khoản SynapLearn của bạn đã được tạo"
                : "Đăng ký tài khoản thành công";
            var message = adminInitiated
                ? "Quản trị viên đã tạo tài khoản cho bạn trên SynapLearn. Bạn có thể đăng nhập bằng email đã đăng ký và đổi mật khẩu sau khi vào hệ thống."
                : "Tài khoản SynapLearn của bạn đã được tạo thành công. Vui lòng kiểm tra email để xác thực OTP trước khi đăng nhập.";

            await PushNotificationAsync(
                user,
                title,
                message,
                category: "account",
                severity: "success",
                source: adminInitiated ? "AdminCreateUser" : "Register",
                actionUrl: "/home/profile.html#notify-center",
                sendEmail: true,
                cancellationToken: cancellationToken,
                persistInInbox: !adminInitiated);
        }

        public async Task EnsureFirstLoginNotificationAsync(
            User user,
            CancellationToken cancellationToken)
        {
            var state = await LoadUserStateAsync(user.UserId, cancellationToken);
            if (state.FirstLoginNotifiedAt.HasValue)
            {
                return;
            }

            await PushNotificationAsync(
                user,
                title: "Chào mừng bạn đến với SynapLearn",
                message: "Đây là lần đăng nhập đầu tiên của bạn. Hãy ghé mục thông báo hệ thống để theo dõi các cập nhật quan trọng từ quản trị viên và hệ thống.",
                category: "account",
                severity: "info",
                source: "FirstLogin",
                actionUrl: "/home/profile.html#notify-center",
                sendEmail: false,
                cancellationToken: cancellationToken);

            state.FirstLoginNotifiedAt = DateTime.UtcNow;
            await SaveUserStateAsync(user.UserId, state, user.UserId, cancellationToken);
        }

        public async Task NotifyModerationDecisionAsync(
            int adminUserId,
            Web_Project.Models.Content content,
            string status,
            string reason,
            CancellationToken cancellationToken)
        {
            if (!content.UserId.HasValue)
            {
                return;
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == content.UserId.Value, cancellationToken);
            if (user is null)
            {
                return;
            }

            var normalizedStatus = NormalizeModerationStatus(status);
            var isPolicyViolation = ContentModerationPolicy.IsPolicyViolationReason(reason);
            var title = normalizedStatus == "Rejected"
                ? isPolicyViolation
                    ? "Nội dung vi phạm chính sách đã bị từ chối"
                    : "Nội dung của bạn đã bị từ chối"
                : normalizedStatus == "Approved"
                    ? "Nội dung của bạn đã được duyệt"
                    : isPolicyViolation
                        ? "Nội dung của bạn đang chờ duyệt chính sách"
                        : "Nội dung của bạn đang chờ duyệt";
            var message = normalizedStatus == "Rejected"
                ? isPolicyViolation
                    ? $"Nội dung \"{content.FileName}\" vi phạm chính sách hệ thống và đã bị từ chối phân tích/tóm tắt. {BuildReasonSuffix(reason)}"
                    : $"Nội dung \"{content.FileName}\" đã bị từ chối do yếu tố nhạy cảm hoặc chưa phù hợp. {BuildReasonSuffix(reason)}"
                : normalizedStatus == "Approved"
                    ? $"Nội dung \"{content.FileName}\" đã được quản trị viên phê duyệt và có thể tiếp tục sử dụng bình thường."
                    : isPolicyViolation
                        ? $"Nội dung \"{content.FileName}\" có dấu hiệu vi phạm chính sách hệ thống nên đang chờ admin duyệt trước khi được phân tích/tóm tắt. {BuildReasonSuffix(reason)}"
                        : $"Nội dung \"{content.FileName}\" đang được đưa vào hàng chờ kiểm duyệt. {BuildReasonSuffix(reason)}";

            await PushNotificationAsync(
                user,
                title,
                message,
                category: "moderation",
                severity: normalizedStatus == "Rejected" ? "warning" : "info",
                source: "Moderation",
                actionUrl: "/home/upload.html",
                sendEmail: CanSendEmailForCategory(await LoadUserNotificationSettingsAsync(user.UserId, cancellationToken), "moderation"),
                cancellationToken: cancellationToken);

            await AppendDispatchLogAsync(new StoredDispatchLog
            {
                DispatchId = Guid.NewGuid().ToString("N"),
                AdminUserId = adminUserId,
                TargetScope = "user",
                TargetLabel = user.Email,
                RecipientCount = 1,
                Title = title,
                Message = message,
                Category = "moderation",
                Severity = normalizedStatus == "Rejected" ? "warning" : "info",
                ActionUrl = "/home/upload.html",
                EmailRequested = true,
                CreatedAt = DateTime.UtcNow
            }, adminUserId, cancellationToken);
        }

        public async Task NotifyLockStateChangedAsync(
            int adminUserId,
            User user,
            bool isLocked,
            string reason,
            CancellationToken cancellationToken)
        {
            var title = isLocked ? "Tài khoản của bạn đã bị khóa" : "Tài khoản của bạn đã được mở khóa";
            var message = isLocked
                ? $"Quản trị viên đã tạm khóa tài khoản của bạn. {BuildReasonSuffix(reason)}"
                : "Tài khoản của bạn đã được mở khóa và có thể đăng nhập lại bình thường.";

            await PushNotificationAsync(
                user,
                title,
                message,
                category: "security",
                severity: isLocked ? "warning" : "success",
                source: isLocked ? "LockUser" : "UnlockUser",
                actionUrl: "/home/profile.html#notify-center",
                sendEmail: true,
                cancellationToken: cancellationToken);

            await AppendDispatchLogAsync(new StoredDispatchLog
            {
                DispatchId = Guid.NewGuid().ToString("N"),
                AdminUserId = adminUserId,
                TargetScope = "user",
                TargetLabel = user.Email,
                RecipientCount = 1,
                Title = title,
                Message = message,
                Category = "security",
                Severity = isLocked ? "warning" : "success",
                ActionUrl = "/home/profile.html#notify-center",
                EmailRequested = true,
                CreatedAt = DateTime.UtcNow
            }, adminUserId, cancellationToken);
        }

        public async Task NotifyAccountDeletedAsync(
            int adminUserId,
            string email,
            string fullName,
            string username,
            string reason,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var title = "Tài khoản SynapLearn của bạn đã bị xóa";
            var message =
                $"Quản trị viên đã xóa tài khoản SynapLearn của bạn ({username}). {BuildReasonSuffix(reason)} " +
                "Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ bộ phận hỗ trợ.";

            await TrySendEmailAsync(
                email,
                title,
                BuildMailHtml(title, message, "/home/register.html"),
                message,
                cancellationToken);

            await AppendDispatchLogAsync(new StoredDispatchLog
            {
                DispatchId = Guid.NewGuid().ToString("N"),
                AdminUserId = adminUserId,
                TargetScope = "user",
                TargetLabel = email,
                RecipientCount = 1,
                Title = title,
                Message = $"{fullName} ({email})",
                Category = "account",
                Severity = "warning",
                ActionUrl = string.Empty,
                EmailRequested = true,
                CreatedAt = DateTime.UtcNow
            }, adminUserId, cancellationToken);
        }

        public async Task NotifySelfAccountDeletedAsync(
            User user,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var title = "Xác nhận xóa tài khoản SynapLearn";
            var message =
                $"Tài khoản SynapLearn của bạn ({user.Username}) đã được xóa theo yêu cầu. " +
                "Nếu đây không phải thao tác của bạn, vui lòng liên hệ bộ phận hỗ trợ ngay.";

            await TrySendEmailAsync(
                user.Email,
                title,
                BuildMailHtml(title, message, "/home/register.html"),
                message,
                cancellationToken);
        }

        private async Task PushNotificationAsync(
            User user,
            string title,
            string message,
            string category,
            string severity,
            string source,
            string actionUrl,
            bool sendEmail,
            CancellationToken cancellationToken,
            bool persistInInbox = true)
        {
            if (persistInInbox)
            {
                var inboxSetting = await GetUserInboxSettingAsync(user.UserId, cancellationToken);
                var inbox = DeserializeList<StoredSystemNotification>(inboxSetting?.SettingValue);
                inbox.Insert(0, new StoredSystemNotification
                {
                    NotificationId = Guid.NewGuid().ToString("N"),
                    Title = TrimTo(title, 140),
                    Message = TrimTo(message, 1800),
                    Category = NormalizeCategory(category),
                    Severity = NormalizeSeverity(severity),
                    ActionUrl = NormalizeActionUrl(actionUrl),
                    Source = TrimTo(source, 64),
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                inbox = inbox
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(MaxInboxItems)
                    .ToList();

                await UpsertSettingAsync(
                    BuildUserInboxKey(user.UserId),
                    inboxSetting,
                    JsonSerializer.Serialize(inbox, JsonOptions),
                    "User system notification inbox",
                    user.UserId,
                    cancellationToken);
            }

            if (sendEmail && !string.IsNullOrWhiteSpace(user.Email))
            {
                await TrySendEmailAsync(
                    user.Email,
                    TrimTo(title, 140),
                    BuildMailHtml(title, message, actionUrl),
                    message,
                    cancellationToken);
            }
        }

        private async Task<List<StoredSystemNotification>> LoadUserInboxAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var setting = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == BuildUserInboxKey(userId))
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            return DeserializeList<StoredSystemNotification>(setting)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        private static UserSystemNotificationInboxResponse BuildInboxResponse(
            IReadOnlyList<StoredSystemNotification> inbox)
        {
            return new UserSystemNotificationInboxResponse
            {
                TotalItems = inbox.Count,
                UnreadCount = inbox.Count(x => !x.IsRead),
                Items = inbox.Select(x => new UserSystemNotificationItemResponse
                {
                    NotificationId = x.NotificationId,
                    Title = x.Title,
                    Message = x.Message,
                    Category = x.Category,
                    Severity = x.Severity,
                    ActionUrl = x.ActionUrl,
                    Source = x.Source,
                    IsRead = x.IsRead,
                    CreatedAt = x.CreatedAt,
                    ReadAt = x.ReadAt
                }).ToList()
            };
        }

        private async Task<SystemSetting?> GetUserInboxSettingAsync(int userId, CancellationToken cancellationToken)
        {
            return await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == BuildUserInboxKey(userId), cancellationToken);
        }

        private async Task<NotificationSettingsResponse> LoadUserNotificationSettingsAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var raw = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == $"user:{userId}:notifications")
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new NotificationSettingsResponse();
            }

            try
            {
                return JsonSerializer.Deserialize<NotificationSettingsResponse>(raw, JsonOptions) ?? new NotificationSettingsResponse();
            }
            catch
            {
                return new NotificationSettingsResponse();
            }
        }

        private static bool CanSendEmailForCategory(NotificationSettingsResponse settings, string category)
        {
            var normalized = NormalizeCategory(category);
            return normalized switch
            {
                "announcement" => settings.NotifyProductNews,
                "moderation" => settings.NotifyReviewReminder,
                "quiz" => settings.NotifyQuizResult,
                _ => true
            };
        }

        private async Task<List<RecipientContext>> ResolveRecipientsAsync(
            string scope,
            int? requestedUserId,
            string? requestedUserEmail,
            CancellationToken cancellationToken)
        {
            if (scope == "user")
            {
                var normalizedEmail = TrimTo(requestedUserEmail, 256).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedEmail) && !requestedUserId.HasValue)
                {
                    return [];
                }

                var usersQuery = _dbContext.Users
                    .AsNoTracking()
                    .Where(x => !string.Equals(x.Role.RoleName, "Admin"));

                User? user = null;
                if (!string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    user = await usersQuery
                        .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
                }
                else if (requestedUserId.HasValue)
                {
                    user = await usersQuery
                        .FirstOrDefaultAsync(x => x.UserId == requestedUserId.Value, cancellationToken);
                }

                if (user is null)
                {
                    return [];
                }

                var settings = await LoadUserNotificationSettingsAsync(user.UserId, cancellationToken);
                return [new RecipientContext(user, settings)];
            }

            var users = await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .Where(x => x.Role.RoleName == "User")
                .OrderBy(x => x.UserId)
                .ToListAsync(cancellationToken);

            var result = new List<RecipientContext>(users.Count);
            foreach (var user in users)
            {
                var settings = await LoadUserNotificationSettingsAsync(user.UserId, cancellationToken);
                result.Add(new RecipientContext(user, settings));
            }

            return result;
        }

        private async Task<StoredNotificationState> LoadUserStateAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            var raw = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == BuildUserStateKey(userId))
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            try
            {
                return JsonSerializer.Deserialize<StoredNotificationState>(raw ?? string.Empty, JsonOptions) ?? new StoredNotificationState();
            }
            catch
            {
                return new StoredNotificationState();
            }
        }

        private async Task SaveUserStateAsync(
            int userId,
            StoredNotificationState state,
            int updatedByUserId,
            CancellationToken cancellationToken)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == BuildUserStateKey(userId), cancellationToken);

            await UpsertSettingAsync(
                BuildUserStateKey(userId),
                setting,
                JsonSerializer.Serialize(state, JsonOptions),
                "User notification state",
                updatedByUserId,
                cancellationToken);
        }

        private async Task<List<StoredDispatchLog>> LoadDispatchLogsAsync(CancellationToken cancellationToken)
        {
            var raw = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == DispatchHistoryKey)
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            return DeserializeList<StoredDispatchLog>(raw)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        private async Task AppendDispatchLogAsync(
            StoredDispatchLog log,
            int adminUserId,
            CancellationToken cancellationToken)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == DispatchHistoryKey, cancellationToken);
            var logs = DeserializeList<StoredDispatchLog>(setting?.SettingValue);
            logs.Insert(0, log);
            logs = logs
                .OrderByDescending(x => x.CreatedAt)
                .Take(MaxDispatchLogItems)
                .ToList();

            await UpsertSettingAsync(
                DispatchHistoryKey,
                setting,
                JsonSerializer.Serialize(logs, JsonOptions),
                "Admin notification dispatch history",
                adminUserId,
                cancellationToken);
        }

        private async Task UpsertSettingAsync(
            string settingKey,
            SystemSetting? setting,
            string settingValue,
            string description,
            int updatedByUserId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (setting is null)
            {
                setting = new SystemSetting
                {
                    SettingKey = settingKey,
                    SettingValue = settingValue,
                    Description = description,
                    UpdatedByUserId = updatedByUserId,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbContext.SystemSettings.Add(setting);
            }
            else
            {
                setting.SettingValue = settingValue;
                setting.Description = description;
                setting.UpdatedByUserId = updatedByUserId;
                setting.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task TrySendEmailAsync(
            string email,
            string subject,
            string htmlBody,
            string textBody,
            CancellationToken cancellationToken)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, htmlBody, textBody, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send system notification email to {Email}. Subject={Subject}", email, subject);
            }
        }

        private static string BuildMailHtml(string title, string message, string actionUrl)
        {
            var actionMarkup = string.IsNullOrWhiteSpace(actionUrl)
                ? string.Empty
                : $"<p style=\"margin-top:20px\"><a href=\"{actionUrl}\" style=\"display:inline-block;padding:10px 16px;background:#1a7ef7;color:#fff;text-decoration:none;border-radius:10px\">Mở SynapLearn</a></p>";

            return
                $"<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:640px;margin:0 auto;padding:24px;color:#0f172a\">" +
                $"<h2 style=\"margin:0 0 12px\">{System.Net.WebUtility.HtmlEncode(title)}</h2>" +
                $"<p style=\"margin:0;line-height:1.7\">{System.Net.WebUtility.HtmlEncode(message)}</p>" +
                actionMarkup +
                "</div>";
        }

        private static List<T> DeserializeList<T>(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<T>>(raw, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static AdminSystemNotificationLogItemResponse MapDispatchLog(StoredDispatchLog log)
        {
            return new AdminSystemNotificationLogItemResponse
            {
                DispatchId = log.DispatchId,
                AdminUserId = log.AdminUserId,
                TargetScope = log.TargetScope,
                TargetLabel = log.TargetLabel,
                RecipientCount = log.RecipientCount,
                Title = log.Title,
                Message = log.Message,
                Category = log.Category,
                Severity = log.Severity,
                ActionUrl = log.ActionUrl,
                EmailRequested = log.EmailRequested,
                CreatedAt = log.CreatedAt
            };
        }

        private static string BuildUserInboxKey(int userId) => $"user:{userId}:system-inbox";
        private static string BuildUserStateKey(int userId) => $"user:{userId}:notification-state";

        private static string NormalizeTargetScope(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "user" ? "user" : "all-users";
        }

        private static string NormalizeCategory(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "announcement" => "announcement",
                "moderation" => "moderation",
                "account" => "account",
                "security" => "security",
                "quiz" => "quiz",
                _ => "announcement"
            };
        }

        private static string NormalizeSeverity(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "success" => "success",
                "warning" => "warning",
                "danger" => "danger",
                _ => "info"
            };
        }

        private static string NormalizeModerationStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "approved" => "Approved",
                "rejected" => "Rejected",
                _ => "Pending"
            };
        }

        private static string BuildReasonSuffix(string? reason)
        {
            var value = (reason ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : $"Lý do: {value}";
        }

        private static string NormalizeActionUrl(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (text.StartsWith("/", StringComparison.Ordinal))
            {
                return TrimTo(text, 256);
            }

            return string.Empty;
        }

        private static string TrimTo(string? value, int maxLength)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= maxLength ? text : text[..maxLength];
        }

        private sealed class RecipientContext
        {
            public RecipientContext(User user, NotificationSettingsResponse settings)
            {
                User = user;
                Settings = settings;
            }

            public User User { get; }

            public NotificationSettingsResponse Settings { get; }
        }

        private sealed class StoredSystemNotification
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

        private sealed class StoredNotificationState
        {
            public DateTime? FirstLoginNotifiedAt { get; set; }
        }

        private sealed class StoredDispatchLog
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
}
