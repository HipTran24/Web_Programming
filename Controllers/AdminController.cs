using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Models.Dtos.Admin;
using Web_Project.Models.Dtos.User;
using Web_Project.Security;
using Web_Project.Services.AI;
using Web_Project.Services.Content;
using Web_Project.Services.Notifications;
using Web_Project.Services.Premium;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private const int MaxPhoneLength = 32;
        private const int MaxBioLength = 1000;
        private readonly AppDbContext _dbContext;
        private readonly IAiRuntimeSettingsService _aiRuntimeSettingsService;
        private readonly ISummaryProcessingService _summaryProcessingService;
        private readonly ISystemNotificationService? _systemNotificationService;
        private readonly IPremiumPlanSettingsService? _premiumPlanSettingsService;

        public AdminController(
            AppDbContext dbContext,
            IAiRuntimeSettingsService aiRuntimeSettingsService,
            ISummaryProcessingService summaryProcessingService,
            ISystemNotificationService? systemNotificationService = null,
            IPremiumPlanSettingsService? premiumPlanSettingsService = null)
        {
            _dbContext = dbContext;
            _aiRuntimeSettingsService = aiRuntimeSettingsService;
            _summaryProcessingService = summaryProcessingService;
            _systemNotificationService = systemNotificationService;
            _premiumPlanSettingsService = premiumPlanSettingsService;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var now = DateTime.UtcNow;
            var selectedWindow = ResolveAnalyticsWindow(Request.Query, now);
            var sevenDaysAgo = now.AddDays(-7);
            var oneDayAgo = now.AddDays(-1);

            var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
            var totalAdmins = await _dbContext.Users.CountAsync(
                x => x.Role.RoleName == "Admin",
                cancellationToken);
            var lockedUsers = await _dbContext.Users.CountAsync(x => x.IsLocked, cancellationToken);
            var verifiedUsers = await _dbContext.Users.CountAsync(x => x.IsEmailVerified, cancellationToken);
            var totalContents = await _dbContext.Contents.CountAsync(cancellationToken);
            var totalQuizzes = await _dbContext.Quizzes.CountAsync(cancellationToken);
            var totalAttempts = await _dbContext.QuizAttempts.CountAsync(cancellationToken);
            var pendingModeration = await _dbContext.ContentModerations.CountAsync(x => x.Status == "Pending", cancellationToken);

            var aiLogs24h = await _dbContext.AISystemLogs
                .Where(x => x.CreatedAt >= oneDayAgo)
                .ToListAsync(cancellationToken);

            var aiTotal24h = aiLogs24h.Count;
            var aiErrors24h = aiLogs24h.Count(x => x.IsError);
            var aiAvgTimeMs24h = aiTotal24h == 0
                ? 0
                : Math.Round(aiLogs24h.Average(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero);

            var activeUserIds = await _dbContext.QuizAttempts
                .Where(x => x.UserId.HasValue && x.SubmittedAt >= sevenDaysAgo)
                .Select(x => x.UserId!.Value)
                .Concat(_dbContext.Contents
                    .Where(x => x.UserId.HasValue && x.CreatedAt >= sevenDaysAgo)
                    .Select(x => x.UserId!.Value))
                .Distinct()
                .CountAsync(cancellationToken);

            var usageRows = await _dbContext.DailyUsageCounters
                .Where(x => x.UsageDate >= selectedWindow.StartDateUtc.Date && x.UsageDate < selectedWindow.EndDateUtcExclusive.Date)
                .GroupBy(x => x.UsageDate.Date)
                .Select(g => new
                {
                    Day = g.Key,
                    Uploads = g.Sum(x => x.UploadCount),
                    AiCalls = g.Sum(x => x.AIProcessCount),
                    QuizGenerations = g.Sum(x => x.QuizGenerationCount)
                })
                .ToListAsync(cancellationToken);
            var usageByDay = usageRows.ToDictionary(x => x.Day, x => x);

            var usageTrend = Enumerable.Range(0, selectedWindow.DayCount)
                .Select(offset => selectedWindow.StartDateUtc.Date.AddDays(offset))
                .Select(day =>
                {
                    usageByDay.TryGetValue(day, out var row);
                    return new
                    {
                        day = day.ToString("dd/MM"),
                        uploads = row?.Uploads ?? 0,
                        aiCalls = row?.AiCalls ?? 0,
                        quizGenerations = row?.QuizGenerations ?? 0
                    };
                })
                .ToList();

            var topContributors = await _dbContext.Users
                .AsNoTracking()
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    Contents = x.Contents.Count,
                    Attempts = x.QuizAttempts.Count
                })
                .OrderByDescending(x => x.Contents)
                .ThenByDescending(x => x.Attempts)
                .Take(8)
                .ToListAsync(cancellationToken);

            var recentUserEvents = await _dbContext.Users
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(12)
                .Select(x => new AdminActivityItem
                {
                    At = x.CreatedAt,
                    Kind = "User",
                    Title = $"Người dùng mới: {x.Username}",
                    Meta = x.Email
                })
                .ToListAsync(cancellationToken);

            var recentContentEvents = await _dbContext.Contents
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(12)
                .Select(x => new AdminActivityItem
                {
                    At = x.CreatedAt,
                    Kind = "Content",
                    Title = string.IsNullOrWhiteSpace(x.FileName) ? $"Content #{x.ContentId}" : x.FileName,
                    Meta = x.SourceType
                })
                .ToListAsync(cancellationToken);

            var recentAiEvents = await _dbContext.AISystemLogs
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(12)
                .Select(x => new AdminActivityItem
                {
                    At = x.CreatedAt,
                    Kind = x.IsError ? "AIError" : "AI",
                    Title = x.ActionType,
                    Meta = x.IsError ? "Lỗi" : "Thành công"
                })
                .ToListAsync(cancellationToken);

            var recentActivities = recentUserEvents
                .Concat(recentContentEvents)
                .Concat(recentAiEvents)
                .OrderByDescending(x => x.At)
                .Take(14)
                .ToList();

            return Ok(new
            {
                generatedAt = now,
                selectedWindow = new
                {
                    selectedWindow.Mode,
                    selectedWindow.Offset,
                    selectedWindow.DayCount,
                    startDate = selectedWindow.StartDateUtc,
                    endDate = selectedWindow.EndDateUtcExclusive.AddDays(-1),
                    selectedWindow.Label
                },
                kpis = new
                {
                    totalUsers,
                    totalAdmins,
                    activeUsers7Days = activeUserIds,
                    lockedUsers,
                    verifiedUsers,
                    totalContents,
                    totalQuizzes,
                    totalAttempts,
                    pendingModeration,
                    aiErrors24h,
                    aiTotal24h,
                    aiErrorRate24h = aiTotal24h == 0
                        ? 0
                        : Math.Round(aiErrors24h * 100d / aiTotal24h, 1, MidpointRounding.AwayFromZero),
                    aiAvgTimeMs24h
                },
                usageTrend,
                topContributors,
                recentActivities
            });
        }

        [HttpGet("dashboard")]
        public Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        {
            return GetOverview(cancellationToken);
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var admin = await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == currentAdminId.Value, cancellationToken);

            if (admin is null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản quản trị." });
            }

            var profileMeta = await LoadProfileMetaAsync(admin.UserId, cancellationToken);
            var profileStats = await GetProfileStatsAsync(admin.UserId, cancellationToken);
            var adminStats = await GetAdminProfileStatsAsync(admin.UserId, cancellationToken);

            return Ok(BuildAdminProfilePayload(admin, profileMeta, profileStats, adminStats));
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var admin = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == currentAdminId.Value, cancellationToken);

            if (admin is null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản quản trị." });
            }

            var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
            var fullName = (request.FullName ?? string.Empty).Trim();
            var phone = TrimTo((request.Phone ?? string.Empty).Trim(), MaxPhoneLength);
            var bio = TrimTo((request.Bio ?? string.Empty).Trim(), MaxBioLength);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return BadRequest(new { message = "Họ tên không được để trống." });
            }

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return BadRequest(new { message = "Email không được để trống." });
            }

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                    x => x.UserId != admin.UserId && x.Email == normalizedEmail,
                    cancellationToken))
            {
                return BadRequest(new { message = "Email đã được sử dụng bởi tài khoản khác." });
            }

            admin.FullName = fullName;
            admin.Email = normalizedEmail;

            await UpsertProfileMetaAsync(admin.UserId, phone, bio, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(
                admin.UserId,
                "UpdateProfile",
                "User",
                admin.UserId.ToString(),
                JsonSerializer.Serialize(new
                {
                    admin.FullName,
                    admin.Email,
                    phone,
                    bio
                }),
                cancellationToken);

            var profileMeta = await LoadProfileMetaAsync(admin.UserId, cancellationToken);
            var profileStats = await GetProfileStatsAsync(admin.UserId, cancellationToken);
            var adminStats = await GetAdminProfileStatsAsync(admin.UserId, cancellationToken);

            return Ok(new
            {
                message = "Cập nhật hồ sơ quản trị thành công.",
                profile = BuildAdminProfile(admin, profileMeta, profileStats),
                adminStats
            });
        }

        [HttpPut("profile/password")]
        public async Task<IActionResult> ChangeProfilePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var admin = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == currentAdminId.Value, cancellationToken);

            if (admin is null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản quản trị." });
            }

            if (!PasswordHashUtility.VerifyPassword(request.CurrentPassword, admin.PasswordHash))
            {
                return BadRequest(new { message = "Mật khẩu hiện tại không chính xác." });
            }

            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu hiện tại." });
            }

            if (!HasStrongPassword(request.NewPassword))
            {
                return BadRequest(new { message = "Mật khẩu mới cần tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số." });
            }

            admin.PasswordHash = PasswordHashUtility.HashPassword(request.NewPassword);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(
                admin.UserId,
                "ChangePassword",
                "User",
                admin.UserId.ToString(),
                JsonSerializer.Serialize(new
                {
                    scope = "AdminProfile"
                }),
                cancellationToken);

            return Ok(new { message = "Đổi mật khẩu quản trị thành công." });
        }

        [HttpGet("admin-users")]
        public async Task<IActionResult> GetAdminUsers(CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var items = await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .Where(x => x.Role.RoleName == "Admin")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    x.IsLocked,
                    x.IsEmailVerified,
                    x.CreatedAt,
                    contentsCount = x.Contents.Count,
                    quizAttemptsCount = x.QuizAttempts.Count
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                totalItems = items.Count,
                activeItems = items.Count(x => !x.IsLocked),
                lockedItems = items.Count(x => x.IsLocked),
                items
            });
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetSystemNotificationHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            if (_systemNotificationService is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ thông báo hệ thống chưa sẵn sàng." });
            }

            var history = await _systemNotificationService.GetDispatchHistoryAsync(page, pageSize, cancellationToken);
            return Ok(history);
        }

        [HttpPost("notifications")]
        public async Task<IActionResult> SendSystemNotification(
            [FromBody] AdminSendSystemNotificationRequest request,
            CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            if (_systemNotificationService is null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ thông báo hệ thống chưa sẵn sàng." });
            }

            var result = await _systemNotificationService.SendCustomAsync(currentAdminId.Value, request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            await AddAuditLogAsync(
                currentAdminId.Value,
                "SendNotification",
                "SystemNotification",
                string.IsNullOrWhiteSpace(request.UserEmail) ? (request.UserId?.ToString() ?? request.TargetScope) : request.UserEmail,
                JsonSerializer.Serialize(new
                {
                    request.TargetScope,
                    request.UserId,
                    request.UserEmail,
                    request.Title,
                    request.Category,
                    request.Severity,
                    request.ActionUrl,
                    request.SendEmail,
                    result.RecipientCount
                }),
                cancellationToken);

            return Ok(new
            {
                message = result.Message,
                recipientCount = result.RecipientCount
            });
        }

        [HttpPost("admin-users")]
        public async Task<IActionResult> CreateAdminUser(
            [FromBody] AdminCreateAccountRequest request,
            CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var username = (request.Username ?? string.Empty).Trim();
            var fullName = (request.FullName ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
            var password = request.Password ?? string.Empty;
            var confirmPassword = request.ConfirmPassword ?? string.Empty;

            var validationErrors = new Dictionary<string, string[]>();
            var emailValidator = new EmailAddressAttribute();

            if (string.IsNullOrWhiteSpace(username))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Username)] = ["Tên đăng nhập không được để trống."];
            }
            else if (username.Length > 64)
            {
                validationErrors[nameof(AdminCreateAccountRequest.Username)] = ["Tên đăng nhập tối đa 64 ký tự."];
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                validationErrors[nameof(AdminCreateAccountRequest.FullName)] = ["Họ tên không được để trống."];
            }
            else if (fullName.Length > 128)
            {
                validationErrors[nameof(AdminCreateAccountRequest.FullName)] = ["Họ tên tối đa 128 ký tự."];
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Email)] = ["Email không được để trống."];
            }
            else if (email.Length > 256 || !emailValidator.IsValid(email))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Email)] = ["Email không hợp lệ."];
            }

            if (!HasStrongPassword(password))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Password)] =
                    ["Mật khẩu cần có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số."];
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
            {
                validationErrors[nameof(AdminCreateAccountRequest.ConfirmPassword)] = ["Xác nhận mật khẩu không khớp."];
            }

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Username == username, cancellationToken))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Username)] = ["Tên đăng nhập đã tồn tại."];
            }

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == email, cancellationToken))
            {
                validationErrors[nameof(AdminCreateAccountRequest.Email)] = ["Email đã được sử dụng."];
            }

            if (validationErrors.Count > 0)
            {
                foreach (var pair in validationErrors)
                {
                    foreach (var message in pair.Value)
                    {
                        ModelState.AddModelError(pair.Key, message);
                    }
                }

                return ValidationProblem(ModelState);
            }

            var adminRoleId = await EnsureRoleIdAsync("Admin", cancellationToken);
            var now = DateTime.UtcNow;

            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordHash = PasswordHashUtility.HashPassword(password),
                RoleId = adminRoleId,
                Status = true,
                IsLocked = false,
                IsEmailVerified = true,
                CreatedAt = now,
                IsPremium = true,
                SubscriptionTier = "Premium"
            };

            _dbContext.Users.Add(user);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "Tên đăng nhập hoặc email đã tồn tại." });
            }

            await AddAuditLogAsync(
                adminUserId: currentAdminId.Value,
                actionType: "CreateAdmin",
                targetType: "User",
                targetId: user.UserId.ToString(),
                detailJson: JsonSerializer.Serialize(new
                {
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = "Admin"
                }),
                cancellationToken: cancellationToken);

            return Created($"/api/admin/admin-users/{user.UserId}", new
            {
                message = "Đã tạo tài khoản admin mới.",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = "Admin",
                    user.IsLocked,
                    user.IsEmailVerified,
                    user.CreatedAt
                }
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? query,
            [FromQuery] string? status,
            [FromQuery] string? role,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 100);
            var loweredQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedStatus = (status ?? "all").Trim().ToLowerInvariant();
            var normalizedRole = (role ?? "all").Trim().ToLowerInvariant();

            var usersQuery = _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(loweredQuery))
            {
                usersQuery = usersQuery.Where(x =>
                    (x.Username ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.FullName ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.Email ?? string.Empty).ToLower().Contains(loweredQuery));
            }

            if (normalizedStatus == "locked")
            {
                usersQuery = usersQuery.Where(x => x.IsLocked);
            }
            else if (normalizedStatus == "active")
            {
                usersQuery = usersQuery.Where(x => !x.IsLocked);
            }

            if (!string.IsNullOrWhiteSpace(normalizedRole) && normalizedRole != "all")
            {
                usersQuery = usersQuery.Where(x => (x.Role.RoleName ?? string.Empty).ToLower() == normalizedRole);
            }

            var totalItems = await usersQuery.CountAsync(cancellationToken);

            var items = await usersQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    role = x.Role.RoleName,
                    x.IsLocked,
                    x.IsEmailVerified,
                    x.IsPremium,
                    x.SubscriptionTier,
                    x.PremiumStartedAt,
                    x.PremiumExpiresAt,
                    x.CreatedAt,
                    hasPremiumHistory = x.UserSubscriptions.Any(),
                    latestSubscriptionExpiresAt = x.UserSubscriptions
                        .OrderByDescending(s => s.ExpiresAt)
                        .Select(s => (DateTime?)s.ExpiresAt)
                        .FirstOrDefault(),
                    latestSubscriptionIsActive = x.UserSubscriptions
                        .OrderByDescending(s => s.ExpiresAt)
                        .Select(s => (bool?)s.IsActive)
                        .FirstOrDefault(),
                    contentsCount = x.Contents.Count,
                    quizAttemptsCount = x.QuizAttempts.Count,
                    lastQuizAt = x.QuizAttempts
                        .OrderByDescending(q => q.SubmittedAt)
                        .Select(q => (DateTime?)q.SubmittedAt)
                        .FirstOrDefault(),
                    lastContentAt = x.Contents
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => (DateTime?)c.CreatedAt)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items = items.Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    x.role,
                    x.IsLocked,
                    x.IsEmailVerified,
                    x.CreatedAt,
                    x.contentsCount,
                    x.quizAttemptsCount,
                    x.lastQuizAt,
                    x.lastContentAt,
                    premium = BuildPremiumState(
                        x.IsPremium,
                        x.SubscriptionTier,
                        x.PremiumStartedAt,
                        x.PremiumExpiresAt,
                        x.hasPremiumHistory,
                        x.latestSubscriptionExpiresAt,
                        x.latestSubscriptionIsActive == true,
                        now)
                })
            });
        }

        [HttpGet("premium/overview")]
        public async Task<IActionResult> GetPremiumOverview(CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var planSettings = await ResolvePremiumPlanSettingsService().GetSettingsAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var paidStatuses = new[] { PaymentTransactionStatuses.Paid, PaymentTransactionStatuses.Success };
            var failedStatuses = new[] { PaymentTransactionStatuses.Failed, PaymentTransactionStatuses.Cancelled };

            var activePremiumUsers = await _dbContext.Users.CountAsync(
                x => (x.IsPremium || x.SubscriptionTier == "Premium") &&
                     (!x.PremiumExpiresAt.HasValue || x.PremiumExpiresAt > now),
                cancellationToken);
            var expiredPremiumUsers = await _dbContext.Users.CountAsync(
                x => (x.IsPremium || x.SubscriptionTier == "Premium" || x.PremiumExpiresAt.HasValue) &&
                     x.PremiumExpiresAt.HasValue &&
                     x.PremiumExpiresAt <= now,
                cancellationToken);
            var pendingTransactions = await _dbContext.PaymentTransactions.CountAsync(
                x => x.Status == PaymentTransactionStatuses.Pending,
                cancellationToken);
            var paidTransactions = await _dbContext.PaymentTransactions.CountAsync(
                x => paidStatuses.Contains(x.Status),
                cancellationToken);
            var failedTransactions = await _dbContext.PaymentTransactions.CountAsync(
                x => failedStatuses.Contains(x.Status),
                cancellationToken);
            var totalRevenue = await _dbContext.PaymentTransactions
                .Where(x => paidStatuses.Contains(x.Status))
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

            return Ok(new
            {
                settings = planSettings,
                metrics = new
                {
                    activePremiumUsers,
                    expiredPremiumUsers,
                    pendingTransactions,
                    paidTransactions,
                    failedTransactions,
                    totalRevenue
                },
                generatedAt = now
            });
        }

        [HttpPut("premium/settings")]
        public async Task<IActionResult> UpdatePremiumSettings(
            [FromBody] AdminPremiumSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            if (request.Amount < 0m)
            {
                return BadRequest(new { message = "Giá Premium không được âm." });
            }

            if (request.Days < 1 || request.Days > 3650)
            {
                return BadRequest(new { message = "Số ngày Premium phải nằm trong khoảng 1 đến 3650." });
            }

            var settings = await ResolvePremiumPlanSettingsService().UpdateSettingsAsync(
                request.Amount,
                request.Days,
                currentAdminId.Value,
                cancellationToken);

            await AddAuditLogAsync(
                currentAdminId.Value,
                "UpdatePremiumSettings",
                "Premium",
                "settings",
                JsonSerializer.Serialize(new { settings.Amount, settings.Days }),
                cancellationToken);

            return Ok(new
            {
                message = "Đã cập nhật giá và thời hạn Premium.",
                settings
            });
        }

        [HttpGet("premium/transactions")]
        public async Task<IActionResult> GetPremiumTransactions(
            [FromQuery] string? query,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 100);
            var loweredQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedStatus = (status ?? "all").Trim().ToLowerInvariant();

            var transactionsQuery = _dbContext.PaymentTransactions
                .AsNoTracking()
                .Include(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(loweredQuery))
            {
                transactionsQuery = transactionsQuery.Where(x =>
                    (x.User.Username ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.User.Email ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.OrderId ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.ProviderReference ?? string.Empty).ToLower().Contains(loweredQuery));
            }

            if (!string.IsNullOrWhiteSpace(normalizedStatus) && normalizedStatus != "all")
            {
                transactionsQuery = transactionsQuery.Where(x => (x.Status ?? string.Empty).ToLower() == normalizedStatus);
            }

            var totalItems = await transactionsQuery.CountAsync(cancellationToken);
            var items = await transactionsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.PaymentTransactionId,
                    x.Provider,
                    x.OrderId,
                    x.RequestId,
                    x.PlanCode,
                    x.PlanName,
                    x.Amount,
                    x.Currency,
                    x.Status,
                    x.ProviderReference,
                    x.ProviderTransactionId,
                    x.ProviderMessage,
                    x.ProviderResultCode,
                    HasPaymentLink = x.PayUrl != null && x.PayUrl != string.Empty,
                    x.CreatedAt,
                    x.PaidAt,
                    x.FailedAt,
                    user = new
                    {
                        x.User.UserId,
                        x.User.Username,
                        x.User.FullName,
                        x.User.Email
                    }
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items
            });
        }

        [HttpGet("premium/users")]
        public async Task<IActionResult> GetPremiumUsers(
            [FromQuery] string? query,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 100);
            var loweredQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedStatus = (status ?? "all").Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;

            var usersQuery = _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(loweredQuery))
            {
                usersQuery = usersQuery.Where(x =>
                    (x.Username ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.FullName ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.Email ?? string.Empty).ToLower().Contains(loweredQuery));
            }

            var rows = await usersQuery
                .OrderByDescending(x => x.PremiumExpiresAt ?? x.CreatedAt)
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    role = x.Role.RoleName,
                    x.IsPremium,
                    x.SubscriptionTier,
                    x.PremiumStartedAt,
                    x.PremiumExpiresAt,
                    x.CreatedAt,
                    lastPaidAt = x.PaymentTransactions
                        .Where(t => t.Status == PaymentTransactionStatuses.Paid || t.Status == PaymentTransactionStatuses.Success)
                        .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
                        .Select(t => (DateTime?)(t.PaidAt ?? t.CreatedAt))
                        .FirstOrDefault(),
                    totalPaid = x.PaymentTransactions
                        .Where(t => t.Status == PaymentTransactionStatuses.Paid || t.Status == PaymentTransactionStatuses.Success)
                        .Sum(t => (decimal?)t.Amount) ?? 0m,
                    hasPremiumHistory = x.UserSubscriptions.Any(),
                    latestSubscriptionExpiresAt = x.UserSubscriptions
                        .OrderByDescending(s => s.ExpiresAt)
                        .Select(s => (DateTime?)s.ExpiresAt)
                        .FirstOrDefault(),
                    latestSubscriptionIsActive = x.UserSubscriptions
                        .OrderByDescending(s => s.ExpiresAt)
                        .Select(s => (bool?)s.IsActive)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var premiumRows = rows
                .Select(x => new
                {
                    x.UserId,
                    x.Username,
                    x.FullName,
                    x.Email,
                    x.role,
                    x.CreatedAt,
                    x.lastPaidAt,
                    x.totalPaid,
                    premium = BuildPremiumState(
                        x.IsPremium,
                        x.SubscriptionTier,
                        x.PremiumStartedAt,
                        x.PremiumExpiresAt,
                        x.hasPremiumHistory,
                        x.latestSubscriptionExpiresAt,
                        x.latestSubscriptionIsActive == true,
                        now)
                })
                .Where(x => normalizedStatus == "all" || x.premium.Status == normalizedStatus)
                .ToList();

            var totalItems = premiumRows.Count;
            var items = premiumRows
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items
            });
        }

        [HttpPost("premium/transactions/{transactionId:int}/approve")]
        public async Task<IActionResult> ApprovePremiumTransaction(
            int transactionId,
            [FromBody] AdminPremiumTransactionActionRequest? request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var transaction = await _dbContext.PaymentTransactions
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.PaymentTransactionId == transactionId, cancellationToken);

            if (transaction is null)
            {
                return NotFound(new { message = "Không tìm thấy giao dịch Premium." });
            }

            if (transaction.Status == PaymentTransactionStatuses.Paid ||
                transaction.Status == PaymentTransactionStatuses.Success)
            {
                return Ok(new
                {
                    message = "Giao dịch đã được duyệt trước đó.",
                    transaction = BuildPremiumTransactionResponse(transaction)
                });
            }

            if (!string.Equals(transaction.Status, PaymentTransactionStatuses.Failed, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ xử lý thủ công giao dịch lỗi. Giao dịch chờ thanh toán cần đợi PayOS webhook." });
            }

            var now = DateTime.UtcNow;
            var planSettings = await ResolvePremiumPlanSettingsService().GetSettingsAsync(cancellationToken);
            transaction.Status = PaymentTransactionStatuses.Paid;
            transaction.PaidAt = now;
            transaction.FailedAt = null;
            transaction.ProviderResultCode = 0;
            transaction.ProviderMessage = TrimTo(
                string.IsNullOrWhiteSpace(request?.Reason)
                    ? "Admin xử lý giao dịch lỗi sau khi đối soát."
                    : $"Admin xử lý giao dịch lỗi: {request.Reason.Trim()}",
                512);
            transaction.UpdatedAt = now;

            await GrantPremiumForTransactionAsync(
                transaction.UserId,
                transaction.PaymentTransactionId,
                planSettings.Days,
                now,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await AddAuditLogAsync(
                currentAdminId.Value,
                "ApprovePremiumTransaction",
                "PaymentTransaction",
                transactionId.ToString(),
                JsonSerializer.Serialize(new
                {
                    transaction.UserId,
                    transaction.OrderId,
                    transaction.Amount,
                    transaction.Provider,
                    days = planSettings.Days,
                    reason = request?.Reason ?? string.Empty
                }),
                cancellationToken);

            return Ok(new
            {
                message = "Đã xử lý giao dịch lỗi và kích hoạt Premium.",
                transaction = BuildPremiumTransactionResponse(transaction)
            });
        }

        [HttpPost("premium/users/{userId:int}/extend")]
        public async Task<IActionResult> ExtendPremiumManually(
            int userId,
            [FromBody] AdminPremiumExtendRequest request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var safeDays = Math.Clamp(request.Days, 1, 3650);
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var now = DateTime.UtcNow;
            var baseDate = user.PremiumExpiresAt.HasValue && user.PremiumExpiresAt.Value > now
                ? user.PremiumExpiresAt.Value
                : now;
            var expiresAt = baseDate.AddDays(safeDays);
            var subscription = await _dbContext.UserSubscriptions
                .Where(x => x.UserId == userId && x.PlanCode == "PREMIUM" && x.IsActive)
                .OrderByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                _dbContext.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    PlanCode = "PREMIUM",
                    StartsAt = now,
                    ExpiresAt = expiresAt,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                subscription.ExpiresAt = expiresAt;
                subscription.IsActive = true;
                subscription.UpdatedAt = now;
            }

            user.IsPremium = true;
            user.SubscriptionTier = "Premium";
            user.PremiumStartedAt ??= now;
            user.PremiumExpiresAt = expiresAt;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await AddAuditLogAsync(
                currentAdminId.Value,
                "ExtendPremium",
                "User",
                userId.ToString(),
                JsonSerializer.Serialize(new { days = safeDays, expiresAt, reason = request.Reason ?? string.Empty }),
                cancellationToken);

            return Ok(new
            {
                message = $"Đã gia hạn Premium thêm {safeDays} ngày.",
                userId,
                premium = BuildPremiumState(
                    user.IsPremium,
                    user.SubscriptionTier,
                    user.PremiumStartedAt,
                    user.PremiumExpiresAt,
                    hasPremiumHistory: true,
                    latestSubscriptionExpiresAt: expiresAt,
                    latestSubscriptionIsActive: true,
                    now: DateTime.UtcNow)
            });
        }

        [HttpPost("premium/users/{userId:int}/cancel")]
        public async Task<IActionResult> CancelPremiumManually(
            int userId,
            [FromBody] AdminPremiumCancelRequest? request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var now = DateTime.UtcNow;
            var subscriptions = await _dbContext.UserSubscriptions
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                subscription.IsActive = false;
                if (subscription.ExpiresAt > now)
                {
                    subscription.ExpiresAt = now;
                }
                subscription.UpdatedAt = now;
            }

            user.IsPremium = false;
            user.SubscriptionTier = "Normal";
            user.PremiumExpiresAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await AddAuditLogAsync(
                currentAdminId.Value,
                "CancelPremium",
                "User",
                userId.ToString(),
                JsonSerializer.Serialize(new { reason = request?.Reason ?? string.Empty }),
                cancellationToken);

            return Ok(new
            {
                message = "Đã hủy Premium thủ công.",
                userId,
                premium = BuildPremiumState(
                    user.IsPremium,
                    user.SubscriptionTier,
                    user.PremiumStartedAt,
                    user.PremiumExpiresAt,
                    hasPremiumHistory: true,
                    latestSubscriptionExpiresAt: now,
                    latestSubscriptionIsActive: false,
                    now: DateTime.UtcNow)
            });
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser(
            [FromBody] AdminUpsertUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var validationErrors = await ValidateUserUpsertRequestAsync(
                request,
                existingUserId: null,
                requirePassword: true,
                cancellationToken);

            if (validationErrors.Count > 0)
            {
                return BuildValidationProblem(validationErrors);
            }

            var roleName = NormalizeRoleName(request.Role);
            var roleId = await EnsureRoleIdAsync(roleName, cancellationToken);

            var user = new User
            {
                Username = request.Username.Trim(),
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                PasswordHash = PasswordHashUtility.HashPassword(request.Password),
                RoleId = roleId,
                Status = true,
                IsLocked = request.IsLocked,
                IsEmailVerified = request.IsEmailVerified,
                CreatedAt = DateTime.UtcNow,
                IsPremium = false,
                SubscriptionTier = "Normal"
            };

            _dbContext.Users.Add(user);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "Tên đăng nhập hoặc email đã tồn tại." });
            }

            await AddAuditLogAsync(
                currentAdminId.Value,
                "CreateUser",
                "User",
                user.UserId.ToString(),
                JsonSerializer.Serialize(new
                {
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = roleName,
                    user.IsLocked,
                    user.IsEmailVerified,
                    user.IsPremium
                }),
                cancellationToken);

            if (string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase) && _systemNotificationService is not null)
            {
                await TryDispatchNotificationAsync(() =>
                    _systemNotificationService.NotifyRegistrationCreatedAsync(user, adminInitiated: true, cancellationToken));
            }

            return Created($"/api/admin/users/{user.UserId}", new
            {
                message = "Đã tạo tài khoản mới.",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = roleName,
                    user.IsLocked,
                    user.IsEmailVerified,
                    user.CreatedAt
                }
            });
        }

        [HttpPut("users/{userId:int}")]
        public async Task<IActionResult> UpdateUser(
            int userId,
            [FromBody] AdminUpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            if (currentAdminId.Value == userId && request.IsLocked)
            {
                return BadRequest(new { message = "Không thể tự khóa tài khoản admin hiện tại." });
            }

            var validationErrors = await ValidateUserUpsertRequestAsync(
                new AdminUpsertUserRequest
                {
                    Username = request.Username,
                    FullName = request.FullName,
                    Email = request.Email,
                    Role = request.Role,
                    IsLocked = request.IsLocked,
                    IsEmailVerified = request.IsEmailVerified
                },
                existingUserId: userId,
                requirePassword: false,
                cancellationToken);

            if (validationErrors.Count > 0)
            {
                return BuildValidationProblem(validationErrors);
            }

            var roleName = NormalizeRoleName(request.Role);
            var wasLocked = user.IsLocked;

            user.Username = request.Username.Trim();
            user.FullName = request.FullName.Trim();
            user.Email = request.Email.Trim().ToLowerInvariant();
            user.RoleId = await EnsureRoleIdAsync(roleName, cancellationToken);
            user.IsLocked = request.IsLocked;
            user.IsEmailVerified = request.IsEmailVerified;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return Conflict(new { message = "Tên đăng nhập hoặc email đã tồn tại." });
            }

            await AddAuditLogAsync(
                currentAdminId.Value,
                "UpdateUser",
                "User",
                user.UserId.ToString(),
                JsonSerializer.Serialize(new
                {
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = roleName,
                    user.IsLocked,
                    user.IsEmailVerified,
                    user.IsPremium
                }),
                cancellationToken);

            if (wasLocked != user.IsLocked &&
                string.Equals(roleName, "User", StringComparison.OrdinalIgnoreCase) &&
                _systemNotificationService is not null)
            {
                await TryDispatchNotificationAsync(() =>
                    _systemNotificationService.NotifyLockStateChangedAsync(
                        currentAdminId.Value,
                        user,
                        user.IsLocked,
                        reason: "Cập nhật từ khu quản trị tài khoản.",
                        cancellationToken));
            }

            return Ok(new
            {
                message = "Đã cập nhật tài khoản.",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.FullName,
                    user.Email,
                    role = roleName,
                    user.IsLocked,
                    user.IsEmailVerified,
                    user.IsPremium
                }
            });
        }

        [HttpDelete("users/{userId:int}")]
        public async Task<IActionResult> DeleteUser(
            int userId,
            [FromBody] AdminDeleteUserRequest? request,
            CancellationToken cancellationToken)
        {
            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            if (currentAdminId.Value == userId)
            {
                return BadRequest(new { message = "Không thể tự xóa tài khoản admin hiện tại." });
            }

            var user = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var snapshot = new
            {
                user.UserId,
                user.Username,
                user.FullName,
                user.Email,
                Role = user.Role.RoleName
            };

            user.Status = false;
            user.IsLocked = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(
                currentAdminId.Value,
                "DeleteUser",
                "User",
                userId.ToString(),
                JsonSerializer.Serialize(new
                {
                    snapshot.UserId,
                    snapshot.Username,
                    snapshot.FullName,
                    snapshot.Email,
                    snapshot.Role,
                    reason = (request?.Reason ?? string.Empty).Trim()
                }),
                cancellationToken);

            if (_systemNotificationService is not null &&
                string.Equals(snapshot.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                await TryDispatchNotificationAsync(() =>
                    _systemNotificationService.NotifyAccountDeletedAsync(
                        currentAdminId.Value,
                        snapshot.Email,
                        snapshot.FullName,
                        snapshot.Username,
                        (request?.Reason ?? string.Empty).Trim(),
                        cancellationToken));
            }

            return Ok(new { message = "Đã ẩn tài khoản người dùng khỏi hệ thống." });
        }

        [HttpPut("users/{userId:int}/lock")]
        public async Task<IActionResult> SetUserLockState(
            int userId,
            [FromBody] AdminUserLockRequest request,
            CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var currentAdminId = TryGetCurrentUserId();
            if (!currentAdminId.HasValue)
            {
                return Unauthorized();
            }

            if (currentAdminId.Value == userId && request.IsLocked)
            {
                return BadRequest(new { message = "Không thể tự khoá tài khoản admin hiện tại." });
            }

            var user = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            user.IsLocked = request.IsLocked;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(
                adminUserId: currentAdminId.Value,
                actionType: request.IsLocked ? "LockUser" : "UnlockUser",
                targetType: "User",
                targetId: userId.ToString(),
                detailJson: $"{{\"reason\":\"{EscapeJson(request.Reason)}\"}}",
                cancellationToken: cancellationToken);

            if (string.Equals(user.Role.RoleName, "User", StringComparison.OrdinalIgnoreCase) &&
                _systemNotificationService is not null)
            {
                await TryDispatchNotificationAsync(() =>
                    _systemNotificationService.NotifyLockStateChangedAsync(
                        currentAdminId.Value,
                        user,
                        request.IsLocked,
                        request.Reason ?? string.Empty,
                        cancellationToken));
            }

            return Ok(new
            {
                message = request.IsLocked ? "Đã khoá tài khoản." : "Đã mở khoá tài khoản.",
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    role = user.Role.RoleName,
                    user.IsLocked
                }
            });
        }

        [HttpGet("contents")]
        public async Task<IActionResult> GetContents(
            [FromQuery] string? query,
            [FromQuery] string? moderationStatus,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 100);
            var loweredQuery = (query ?? string.Empty).Trim().ToLowerInvariant();
            var status = (moderationStatus ?? "all").Trim().ToLowerInvariant();

            var contentsQuery = _dbContext.Contents
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.ContentModeration)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(loweredQuery))
            {
                contentsQuery = contentsQuery.Where(x =>
                    (x.FileName ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.SourceType ?? string.Empty).ToLower().Contains(loweredQuery) ||
                    (x.User != null && (
                        (x.User.Username ?? string.Empty).ToLower().Contains(loweredQuery) ||
                        (x.User.Email ?? string.Empty).ToLower().Contains(loweredQuery))));
            }

            if (status == "pending" || status == "approved" || status == "rejected")
            {
                var normalizedStatus = char.ToUpper(status[0]) + status[1..];
                contentsQuery = contentsQuery.Where(x => x.ContentModeration != null && x.ContentModeration.Status == normalizedStatus);
            }
            else if (status == "none")
            {
                contentsQuery = contentsQuery.Where(x => x.ContentModeration == null);
            }

            var totalItems = await contentsQuery.CountAsync(cancellationToken);

            var items = await contentsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.ContentId,
                    x.FileName,
                    x.SourceType,
                    x.FetchStatus,
                    x.CreatedAt,
                    user = x.User == null
                        ? null
                        : new
                        {
                            x.User.UserId,
                            x.User.Username,
                            x.User.Email
                        },
                    moderation = x.ContentModeration == null
                        ? null
                        : new
                        {
                            x.ContentModeration.ModerationId,
                            x.ContentModeration.Status,
                            x.ContentModeration.Reason,
                            x.ContentModeration.UpdatedAt,
                            x.ContentModeration.ReviewedAt,
                            x.ContentModeration.ReviewedByUserId,
                            isPolicyViolation = (x.ContentModeration.Reason ?? string.Empty).StartsWith(ContentModerationPolicy.PolicyReasonPrefix)
                        },
                    quizCount = x.Quizzes.Count
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items
            });
        }

        [HttpPut("contents/{contentId:int}/moderation")]
        public async Task<IActionResult> UpdateModeration(
            int contentId,
            [FromBody] AdminModerationUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var adminId = TryGetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized();
            }

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Pending",
                "Approved",
                "Rejected"
            };

            if (string.IsNullOrWhiteSpace(request.Status) || !allowedStatuses.Contains(request.Status))
            {
                return BadRequest(new { message = "Trạng thái moderation không hợp lệ." });
            }

            var content = await _dbContext.Contents
                .Include(x => x.ContentModeration)
                .FirstOrDefaultAsync(x => x.ContentId == contentId, cancellationToken);

            if (content is null)
            {
                return NotFound(new { message = "Không tìm thấy nội dung." });
            }

            var now = DateTime.UtcNow;
            var normalizedStatus = NormalizeModerationStatus(request.Status);
            var existingReason = content.ContentModeration?.Reason ?? string.Empty;
            var finalReason = ResolveModerationReason(normalizedStatus, request.Reason, existingReason);

            if (content.ContentModeration is null)
            {
                content.ContentModeration = new ContentModeration
                {
                    ContentId = contentId,
                    Status = normalizedStatus,
                    Reason = finalReason,
                    ReviewedByUserId = adminId.Value,
                    CreatedAt = now,
                    UpdatedAt = now,
                    ReviewedAt = now
                };
                _dbContext.ContentModerations.Add(content.ContentModeration);
            }
            else
            {
                content.ContentModeration.Status = normalizedStatus;
                content.ContentModeration.Reason = finalReason;
                content.ContentModeration.ReviewedByUserId = adminId.Value;
                content.ContentModeration.UpdatedAt = now;
                content.ContentModeration.ReviewedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await AddAuditLogAsync(
                adminUserId: adminId.Value,
                actionType: "ContentModeration",
                targetType: "Content",
                targetId: contentId.ToString(),
                detailJson: $"{{\"status\":\"{EscapeJson(normalizedStatus)}\",\"reason\":\"{EscapeJson(finalReason)}\"}}",
                cancellationToken: cancellationToken);

            var generatedDeferredSummary = false;
            var deferredSummaryError = string.Empty;
            if (string.Equals(normalizedStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    generatedDeferredSummary = await _summaryProcessingService.GenerateApprovedContentSummaryAsync(contentId, cancellationToken);
                }
                catch (Exception ex)
                {
                    deferredSummaryError = ex.Message;
                }
            }

            if (_systemNotificationService is not null)
            {
                await TryDispatchNotificationAsync(() =>
                    _systemNotificationService.NotifyModerationDecisionAsync(
                        adminId.Value,
                        content,
                        normalizedStatus,
                        finalReason,
                        cancellationToken));
            }

            return Ok(new
            {
                message = generatedDeferredSummary
                    ? "Đã cập nhật moderation và tạo tóm tắt cho nội dung đã được duyệt."
                    : string.IsNullOrWhiteSpace(deferredSummaryError)
                        ? "Đã cập nhật moderation."
                        : $"Đã cập nhật moderation nhưng chưa thể tạo tóm tắt ngay: {deferredSummaryError}",
                moderation = new
                {
                    content.ContentModeration.ModerationId,
                    content.ContentModeration.Status,
                    content.ContentModeration.Reason,
                    content.ContentModeration.ReviewedByUserId,
                    content.ContentModeration.ReviewedAt,
                    content.ContentModeration.UpdatedAt
                }
            });
        }

        [HttpGet("ai-settings")]
        public async Task<IActionResult> GetAiSettings(CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var payload = await _aiRuntimeSettingsService.GetAdminSettingsAsync(cancellationToken);
            return Ok(payload);
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAdminAlerts(
            [FromQuery] int limit = 8,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safeLimit = Math.Clamp(limit, 3, 12);
            var now = DateTime.UtcNow;
            var oneDayAgo = now.AddDays(-1);
            var sevenDaysAgo = now.AddDays(-7);

            var pendingModerationCount = await _dbContext.ContentModerations
                .AsNoTracking()
                .CountAsync(x => x.Status == "Pending", cancellationToken);

            var aiIncidentCount24h = await _dbContext.AISystemLogs
                .AsNoTracking()
                .CountAsync(x => x.IsError && x.CreatedAt >= oneDayAgo, cancellationToken);

            var moderationItems = await _dbContext.ContentModerations
                .AsNoTracking()
                .Include(x => x.Content)
                    .ThenInclude(x => x.User)
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.UpdatedAt)
                .Take(safeLimit)
                .Select(x => new
                {
                    x.ModerationId,
                    x.ContentId,
                    x.Reason,
                    x.CreatedAt,
                    x.UpdatedAt,
                    FileName = x.Content.FileName,
                    Username = x.Content.User == null ? string.Empty : x.Content.User.Username
                })
                .ToListAsync(cancellationToken);

            var moderationAlerts = moderationItems
                .Select(x => new
                {
                    alertId = $"moderation:{x.ModerationId}",
                    kind = "moderation",
                    severity = (x.Reason ?? string.Empty).StartsWith(ContentModerationPolicy.PolicyReasonPrefix, StringComparison.Ordinal)
                        ? "danger"
                        : "warning",
                    title = string.IsNullOrWhiteSpace(x.FileName)
                        ? $"Nội dung #{x.ContentId} đang chờ duyệt"
                        : x.FileName,
                    message = BuildModerationAlertMessage(x.Reason, x.Username),
                    createdAt = x.UpdatedAt == default ? x.CreatedAt : x.UpdatedAt,
                    actionUrl = "/admin/content",
                    actionLabel = "Mở trang kiểm duyệt"
                })
                .ToList();

            var incidentItems = await _dbContext.AISystemLogs
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.IsError && x.CreatedAt >= sevenDaysAgo)
                .OrderByDescending(x => x.CreatedAt)
                .Take(safeLimit)
                .Select(x => new
                {
                    x.LogId,
                    x.ActionType,
                    x.CreatedAt,
                    Username = x.User == null ? string.Empty : x.User.Username
                })
                .ToListAsync(cancellationToken);

            var incidentAlerts = incidentItems
                .Select(x => new
                {
                    alertId = $"incident:{x.LogId}",
                    kind = "incident",
                    severity = "danger",
                    title = $"Sự cố AI: {NormalizeAdminAiActionTitle(x.ActionType)}",
                    message = string.IsNullOrWhiteSpace(x.Username)
                        ? "Một yêu cầu AI từ hệ thống hoặc phiên khách vừa gặp lỗi và cần kiểm tra log chi tiết."
                        : $"Yêu cầu AI của @{x.Username} vừa gặp lỗi và cần kiểm tra trên AI logs.",
                    createdAt = x.CreatedAt,
                    actionUrl = "/admin/reports",
                    actionLabel = "Mở AI logs"
                })
                .ToList();

            var items = moderationAlerts
                .Concat(incidentAlerts)
                .OrderByDescending(x => x.createdAt)
                .Take(safeLimit)
                .ToList();

            return Ok(new
            {
                generatedAt = now,
                counts = new
                {
                    pendingModeration = pendingModerationCount,
                    aiIncidents24h = aiIncidentCount24h
                },
                items
            });
        }

        [HttpPut("ai-settings")]
        public async Task<IActionResult> UpdateAiSettings(
            [FromBody] AdminAiSystemSettingsUpdateRequest request,
            CancellationToken cancellationToken)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var adminId = TryGetCurrentUserId();
            if (!adminId.HasValue)
            {
                return Unauthorized();
            }

            var payload = await _aiRuntimeSettingsService.UpdateAdminSettingsAsync(request, adminId.Value, cancellationToken);

            await AddAuditLogAsync(
                adminUserId: adminId.Value,
                actionType: "UpdateAiSettings",
                targetType: "SystemSetting",
                targetId: "system:ai:runtime-config",
                detailJson: JsonSerializer.Serialize(new
                {
                    payload.Routing.PrimaryTextProvider,
                    payload.Routing.PrimaryVisionProvider,
                    payload.Routing.TextOutputTokenBudget,
                    payload.Routing.QuizOutputTokenBudget,
                    payload.Routing.ImageOutputTokenBudget,
                    GeminiTextModel = payload.Gemini.TextModel,
                    GeminiVisionModel = payload.Gemini.VisionModel,
                    GroqTextModel = payload.Groq.TextModel,
                    GroqVisionModel = payload.Groq.VisionModel,
                    GroqAudioModel = payload.Groq.AudioModel
                }),
                cancellationToken: cancellationToken);

            return Ok(new
            {
                message = "Đã cập nhật cấu hình AI hệ thống.",
                settings = payload
            });
        }

        [HttpGet("ai-logs")]
        public async Task<IActionResult> GetAiLogs(
            [FromQuery] bool errorsOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 200);
            var now = DateTime.UtcNow;
            var oneDayAgo = now.AddDays(-1);
            var selectedWindow = ResolveAnalyticsWindow(Request.Query, now);

            var query = _dbContext.AISystemLogs
                .AsNoTracking()
                .Include(x => x.User)
                .AsQueryable();

            if (errorsOnly)
            {
                query = query.Where(x => x.IsError);
            }

            var totalItems = await query.CountAsync(cancellationToken);
            var analyticsRows = await query
                .Where(x => x.CreatedAt >= selectedWindow.StartDateUtc && x.CreatedAt < selectedWindow.EndDateUtcExclusive)
                .Select(x => new AiLogAnalyticsRow
                {
                    ActionType = x.ActionType,
                    IsError = x.IsError,
                    IsGuest = x.IsGuest,
                    ProcessingTime = x.ProcessingTime,
                    CreatedAt = x.CreatedAt,
                    Username = x.User == null ? string.Empty : x.User.Username,
                    Email = x.User == null ? string.Empty : x.User.Email
                })
                .ToListAsync(cancellationToken);

            var last24hRows = await query
                .Where(x => x.CreatedAt >= oneDayAgo)
                .Select(x => new AiLogAnalyticsRow
                {
                    ActionType = x.ActionType,
                    IsError = x.IsError,
                    IsGuest = x.IsGuest,
                    ProcessingTime = x.ProcessingTime,
                    CreatedAt = x.CreatedAt,
                    Username = x.User == null ? string.Empty : x.User.Username,
                    Email = x.User == null ? string.Empty : x.User.Email
                })
                .ToListAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.LogId,
                    x.ActionType,
                    x.IsError,
                    x.IsGuest,
                    x.ProcessingTime,
                    x.CreatedAt,
                    user = x.User == null
                        ? null
                        : new
                        {
                            x.User.UserId,
                            x.User.Username,
                            x.User.Email
                        }
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items,
                selectedWindow = new
                {
                    selectedWindow.Mode,
                    selectedWindow.Offset,
                    selectedWindow.DayCount,
                    startDate = selectedWindow.StartDateUtc,
                    endDate = selectedWindow.EndDateUtcExclusive.AddDays(-1),
                    selectedWindow.Label
                },
                summary = BuildAiWindowSummary(last24hRows, "24h"),
                windowSummary = BuildAiWindowSummary(analyticsRows, selectedWindow.Label),
                latencyTrend = BuildAiLatencyTrend(analyticsRows, selectedWindow.StartDateUtc, selectedWindow.DayCount),
                actionBreakdown = BuildAiActionBreakdown(analyticsRows),
                slowestItems = BuildSlowestAiItems(analyticsRows)
            });
        }

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 5, 200);

            var query = _dbContext.AdminAuditLogs
                .AsNoTracking()
                .Include(x => x.AdminUser)
                .AsQueryable();

            var totalItems = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(x => new
                {
                    x.AuditId,
                    x.ActionType,
                    x.TargetType,
                    x.TargetId,
                    x.DetailJson,
                    x.IpAddress,
                    x.CreatedAt,
                    admin = x.AdminUser == null
                        ? null
                        : new
                        {
                            x.AdminUser.UserId,
                            x.AdminUser.Username,
                            x.AdminUser.Email
                        }
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                page = safePage,
                pageSize = safePageSize,
                totalItems,
                totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)safePageSize)),
                items
            });
        }

        private async Task AddAuditLogAsync(
            int adminUserId,
            string actionType,
            string targetType,
            string targetId,
            string detailJson,
            CancellationToken cancellationToken)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

            _dbContext.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminUserId = adminUserId,
                ActionType = TrimTo(actionType, 64),
                TargetType = TrimTo(targetType, 64),
                TargetId = TrimTo(targetId, 128),
                DetailJson = detailJson ?? string.Empty,
                IpAddress = TrimTo(ipAddress, 64),
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private IPremiumPlanSettingsService ResolvePremiumPlanSettingsService()
        {
            return _premiumPlanSettingsService
                ?? throw new InvalidOperationException("Premium plan settings service is not registered.");
        }

        private async Task GrantPremiumForTransactionAsync(
            int userId,
            int paymentTransactionId,
            int days,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return;
            }

            var safeDays = Math.Max(1, days);
            var baseDate = user.PremiumExpiresAt.HasValue && user.PremiumExpiresAt.Value > now
                ? user.PremiumExpiresAt.Value
                : now;
            var expiresAt = baseDate.AddDays(safeDays);
            var subscription = await _dbContext.UserSubscriptions
                .Where(x => x.UserId == userId && x.PlanCode == "PREMIUM" && x.IsActive)
                .OrderByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                _dbContext.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    PlanCode = "PREMIUM",
                    StartsAt = now,
                    ExpiresAt = expiresAt,
                    IsActive = true,
                    PaymentTransactionId = paymentTransactionId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                subscription.ExpiresAt = expiresAt;
                subscription.IsActive = true;
                subscription.PaymentTransactionId = paymentTransactionId;
                subscription.UpdatedAt = now;
            }

            user.IsPremium = true;
            user.SubscriptionTier = "Premium";
            user.PremiumStartedAt ??= now;
            user.PremiumExpiresAt = expiresAt;
        }

        private static object BuildPremiumTransactionResponse(PaymentTransaction transaction)
        {
            return new
            {
                transaction.PaymentTransactionId,
                transaction.Provider,
                transaction.OrderId,
                transaction.RequestId,
                transaction.PlanCode,
                transaction.PlanName,
                transaction.Amount,
                transaction.Currency,
                transaction.Status,
                transaction.ProviderReference,
                transaction.ProviderTransactionId,
                transaction.ProviderMessage,
                transaction.ProviderResultCode,
                HasPaymentLink = !string.IsNullOrWhiteSpace(transaction.PayUrl),
                transaction.CreatedAt,
                transaction.PaidAt,
                transaction.FailedAt,
                user = transaction.User is null
                    ? null
                    : new
                    {
                        transaction.User.UserId,
                        transaction.User.Username,
                        transaction.User.FullName,
                        transaction.User.Email
                    }
            };
        }

        private static AdminPremiumState BuildPremiumState(
            bool isPremium,
            string subscriptionTier,
            DateTime? premiumStartedAt,
            DateTime? premiumExpiresAt,
            bool hasPremiumHistory,
            DateTime? latestSubscriptionExpiresAt,
            bool latestSubscriptionIsActive,
            DateTime now)
        {
            var tierIsPremium = string.Equals(subscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase);
            var subscriptionIsActive = latestSubscriptionIsActive &&
                latestSubscriptionExpiresAt.HasValue &&
                latestSubscriptionExpiresAt.Value > now;
            var effectiveExpiresAt = premiumExpiresAt ?? latestSubscriptionExpiresAt;
            var hasPremiumMarker = isPremium || tierIsPremium || subscriptionIsActive;
            var isActive = hasPremiumMarker &&
                (!effectiveExpiresAt.HasValue || effectiveExpiresAt.Value > now);
            var hasHistory = hasPremiumHistory ||
                premiumStartedAt.HasValue ||
                premiumExpiresAt.HasValue ||
                latestSubscriptionExpiresAt.HasValue ||
                isPremium ||
                tierIsPremium;

            if (isActive)
            {
                return new AdminPremiumState
                {
                    Status = "active",
                    Label = "Đang Premium",
                    StartedAt = premiumStartedAt,
                    ExpiresAt = effectiveExpiresAt
                };
            }

            if (hasHistory)
            {
                return new AdminPremiumState
                {
                    Status = "expired",
                    Label = "Hết hạn",
                    StartedAt = premiumStartedAt,
                    ExpiresAt = effectiveExpiresAt
                };
            }

            return new AdminPremiumState
            {
                Status = "none",
                Label = "Chưa đăng ký",
                StartedAt = premiumStartedAt,
                ExpiresAt = effectiveExpiresAt
            };
        }

        private async Task<int> EnsureRoleIdAsync(string roleName, CancellationToken cancellationToken)
        {
            var normalizedRoleName = TrimTo((roleName ?? string.Empty).Trim(), 64);
            var existingRole = await _dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RoleName == normalizedRoleName, cancellationToken);

            if (existingRole is not null)
            {
                return existingRole.RoleId;
            }

            var role = new Role
            {
                RoleName = normalizedRoleName
            };

            _dbContext.Roles.Add(role);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return role.RoleId;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _dbContext.Entry(role).State = EntityState.Detached;

                var fallbackRole = await _dbContext.Roles
                    .AsNoTracking()
                    .FirstAsync(x => x.RoleName == normalizedRoleName, cancellationToken);

                return fallbackRole.RoleId;
            }
        }

        private bool IsCurrentUserAdmin()
        {
            return User.Claims.Any(x =>
                x.Type == ClaimTypes.Role &&
                string.Equals(x.Value, "Admin", StringComparison.OrdinalIgnoreCase));
        }

        private int? TryGetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private async Task<Dictionary<string, string[]>> ValidateUserUpsertRequestAsync(
            AdminUpsertUserRequest request,
            int? existingUserId,
            bool requirePassword,
            CancellationToken cancellationToken)
        {
            var username = (request.Username ?? string.Empty).Trim();
            var fullName = (request.FullName ?? string.Empty).Trim();
            var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
            var password = request.Password ?? string.Empty;
            var confirmPassword = request.ConfirmPassword ?? string.Empty;
            var validationErrors = new Dictionary<string, string[]>();
            var emailValidator = new EmailAddressAttribute();

            if (string.IsNullOrWhiteSpace(username))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Username)] = ["Tên đăng nhập không được để trống."];
            }
            else if (username.Length > 64)
            {
                validationErrors[nameof(AdminUpsertUserRequest.Username)] = ["Tên đăng nhập tối đa 64 ký tự."];
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                validationErrors[nameof(AdminUpsertUserRequest.FullName)] = ["Họ tên không được để trống."];
            }
            else if (fullName.Length > 128)
            {
                validationErrors[nameof(AdminUpsertUserRequest.FullName)] = ["Họ tên tối đa 128 ký tự."];
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Email)] = ["Email không được để trống."];
            }
            else if (email.Length > 256 || !emailValidator.IsValid(email))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Email)] = ["Email không hợp lệ."];
            }

            if (!IsAllowedRole(request.Role))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Role)] = ["Vai trò chỉ được là Admin hoặc User."];
            }

            if (requirePassword)
            {
                if (!HasStrongPassword(password))
                {
                    validationErrors[nameof(AdminUpsertUserRequest.Password)] =
                        ["Mật khẩu cần có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số."];
                }

                if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                {
                    validationErrors[nameof(AdminUpsertUserRequest.ConfirmPassword)] = ["Xác nhận mật khẩu không khớp."];
                }
            }

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                    x => x.Username == username && (!existingUserId.HasValue || x.UserId != existingUserId.Value),
                    cancellationToken))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Username)] = ["Tên đăng nhập đã tồn tại."];
            }

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                    x => x.Email == email && (!existingUserId.HasValue || x.UserId != existingUserId.Value),
                    cancellationToken))
            {
                validationErrors[nameof(AdminUpsertUserRequest.Email)] = ["Email đã được sử dụng."];
            }

            return validationErrors;
        }

        private IActionResult BuildValidationProblem(Dictionary<string, string[]> validationErrors)
        {
            foreach (var pair in validationErrors)
            {
                foreach (var message in pair.Value)
                {
                    ModelState.AddModelError(pair.Key, message);
                }
            }

            return ValidationProblem(ModelState);
        }

        private static bool IsAllowedRole(string? value)
        {
            return string.Equals(value, "Admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "User", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRoleName(string? value)
        {
            var lowered = (value ?? "User").Trim().ToLowerInvariant();
            return lowered switch
            {
                "admin" => "Admin",
                "administrator" => "Admin",
                _ => "User"
            };
        }

        private static bool HasStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return false;
            }

            return Regex.IsMatch(password, "[A-Z]") &&
                   Regex.IsMatch(password, "[a-z]") &&
                   Regex.IsMatch(password, "[0-9]");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

        private static string EscapeJson(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal);
        }

        private static string ResolveModerationReason(string status, string? requestedReason, string? existingReason)
        {
            var normalizedStatus = NormalizeModerationStatus(status);
            if (ContentModerationPolicy.IsPolicyViolationReason(existingReason))
            {
                return normalizedStatus switch
                {
                    "Rejected" => ContentModerationPolicy.EnsurePolicyViolationReason(requestedReason, existingReason),
                    "Pending" => string.IsNullOrWhiteSpace(requestedReason) ? existingReason?.Trim() ?? string.Empty : ContentModerationPolicy.EnsurePolicyViolationReason(requestedReason, existingReason),
                    _ => (requestedReason ?? string.Empty).Trim()
                };
            }

            return (requestedReason ?? string.Empty).Trim();
        }

        private static string TrimTo(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private static string NormalizeModerationStatus(string status)
        {
            var trimmed = (status ?? string.Empty).Trim();
            if (trimmed.Equals("approved", StringComparison.OrdinalIgnoreCase))
            {
                return "Approved";
            }

            if (trimmed.Equals("rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "Rejected";
            }

            return "Pending";
        }

        private async Task<AdminProfileMeta> LoadProfileMetaAsync(int userId, CancellationToken cancellationToken)
        {
            var key = BuildProfileMetaSettingKey(userId);
            var raw = await _dbContext.SystemSettings
                .AsNoTracking()
                .Where(x => x.SettingKey == key)
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new AdminProfileMeta();
            }

            try
            {
                return JsonSerializer.Deserialize<AdminProfileMeta>(raw) ?? new AdminProfileMeta();
            }
            catch
            {
                return new AdminProfileMeta();
            }
        }

        private async Task UpsertProfileMetaAsync(
            int userId,
            string phone,
            string bio,
            CancellationToken cancellationToken)
        {
            var key = BuildProfileMetaSettingKey(userId);
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);

            var now = DateTime.UtcNow;
            var payload = JsonSerializer.Serialize(new AdminProfileMeta
            {
                Phone = phone,
                Bio = bio
            });

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

        private async Task<AdminProfileStatsSnapshot> GetAdminProfileStatsAsync(int userId, CancellationToken cancellationToken)
        {
            var totalAuditActions = await _dbContext.AdminAuditLogs
                .CountAsync(x => x.AdminUserId == userId, cancellationToken);

            var managedUsers = await _dbContext.AdminAuditLogs
                .Where(x => x.AdminUserId == userId && x.TargetType == "User" && x.TargetId != string.Empty)
                .Select(x => x.TargetId)
                .Distinct()
                .CountAsync(cancellationToken);

            var reviewedContents = await _dbContext.ContentModerations
                .CountAsync(x => x.ReviewedByUserId == userId, cancellationToken);

            var createdAdmins = await _dbContext.AdminAuditLogs
                .CountAsync(x => x.AdminUserId == userId && x.ActionType == "CreateAdmin", cancellationToken);

            var lastAdminActionAt = await _dbContext.AdminAuditLogs
                .Where(x => x.AdminUserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return new AdminProfileStatsSnapshot
            {
                TotalAuditActions = totalAuditActions,
                ManagedUsers = managedUsers,
                ReviewedContents = reviewedContents,
                CreatedAdmins = createdAdmins,
                LastAdminActionAt = lastAdminActionAt
            };
        }

        private object BuildAdminProfilePayload(
            User admin,
            AdminProfileMeta profileMeta,
            ProfileStatsSnapshot profileStats,
            AdminProfileStatsSnapshot adminStats)
        {
            return new
            {
                profile = BuildAdminProfile(admin, profileMeta, profileStats),
                adminStats
            };
        }

        private static AnalyticsWindow ResolveAnalyticsWindow(IQueryCollection query, DateTime nowUtc)
        {
            var mode = (query["windowMode"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedMode = mode is "rolling" or "custom" ? mode : "week";

            var offset = 0;
            if (int.TryParse(query["windowOffset"], out var parsedOffset))
            {
                offset = Math.Clamp(parsedOffset, 0, 24);
            }

            if (normalizedMode == "custom")
            {
                var hasStart = DateTime.TryParse(query["windowStart"], out var startDate);
                var hasEnd = DateTime.TryParse(query["windowEnd"], out var endDate);

                if (hasStart && hasEnd)
                {
                    var normalizedStart = startDate.Date;
                    var normalizedEnd = endDate.Date;
                    if (normalizedEnd >= normalizedStart)
                    {
                        var customDayCount = Math.Clamp((normalizedEnd - normalizedStart).Days + 1, 1, 62);
                        var customEndExclusive = normalizedStart.AddDays(customDayCount);
                        return new AnalyticsWindow(
                            normalizedStart,
                            customEndExclusive,
                            normalizedMode,
                            0,
                            customDayCount,
                            $"Tùy chỉnh • {normalizedStart:dd/MM} - {customEndExclusive.AddDays(-1):dd/MM}");
                    }
                }
            }

            if (normalizedMode == "rolling")
            {
                var dayCount = 7;
                if (int.TryParse(query["windowDays"], out var parsedDays))
                {
                    dayCount = Math.Clamp(parsedDays, 7, 62);
                }

                var startDate = nowUtc.Date.AddDays(-(dayCount - 1) - (offset * dayCount));
                var endDateExclusive = startDate.AddDays(dayCount);
                var prefix = offset == 0 ? $"{dayCount} ngày gần nhất" : $"{dayCount} ngày trước đó";
                var label = $"{prefix} • {startDate:dd/MM} - {endDateExclusive.AddDays(-1):dd/MM}";

                return new AnalyticsWindow(startDate, endDateExclusive, normalizedMode, offset, dayCount, label);
            }

            var currentWeekStart = StartOfWeek(nowUtc.Date, DayOfWeek.Monday);
            var weekStart = currentWeekStart.AddDays(-(offset * 7));
            var weekEndExclusive = weekStart.AddDays(7);
            var weekLabel = offset switch
            {
                0 => $"Tuần này • {weekStart:dd/MM} - {weekEndExclusive.AddDays(-1):dd/MM}",
                1 => $"Tuần trước • {weekStart:dd/MM} - {weekEndExclusive.AddDays(-1):dd/MM}",
                _ => $"{offset} tuần trước • {weekStart:dd/MM} - {weekEndExclusive.AddDays(-1):dd/MM}"
            };

            return new AnalyticsWindow(weekStart, weekEndExclusive, normalizedMode, offset, 7, weekLabel);
        }

        private static DateTime StartOfWeek(DateTime value, DayOfWeek firstDayOfWeek)
        {
            var diff = (7 + (value.DayOfWeek - firstDayOfWeek)) % 7;
            return value.AddDays(-diff).Date;
        }

        private static object BuildAiWindowSummary(
            IReadOnlyCollection<AiLogAnalyticsRow> rows,
            string label)
        {
            var total = rows.Count;
            var errors = rows.Count(x => x.IsError);
            var avgLatencyMs = total == 0 ? 0 : Math.Round(rows.Average(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero);
            var p95LatencyMs = CalculatePercentileMs(rows.Select(x => x.ProcessingTime), 0.95);
            var maxLatencyMs = total == 0 ? 0 : Math.Round(rows.Max(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero);
            var errorRate = total == 0 ? 0 : Math.Round(errors * 100d / total, 1, MidpointRounding.AwayFromZero);
            var guestCalls = rows.Count(x => x.IsGuest);

            return new
            {
                label,
                totalLogs = total,
                errors,
                errorRate,
                avgLatencyMs,
                p95LatencyMs,
                maxLatencyMs,
                guestCalls,
                totalLogs7Days = total,
                totalLogs24h = total,
                errors24h = errors,
                errorRate24h = errorRate,
                avgLatencyMs24h = avgLatencyMs,
                p95LatencyMs24h = p95LatencyMs,
                maxLatencyMs24h = maxLatencyMs,
                guestCalls24h = guestCalls
            };
        }

        private static IReadOnlyList<object> BuildAiLatencyTrend(
            IReadOnlyCollection<AiLogAnalyticsRow> rows,
            DateTime startDateUtc,
            int dayCount)
        {
            var groupedByDay = rows
                .GroupBy(x => x.CreatedAt.Date)
                .ToDictionary(x => x.Key, x => x.ToArray());

            return Enumerable.Range(0, dayCount)
                .Select(offset => startDateUtc.Date.AddDays(offset))
                .Select(day =>
                {
                    groupedByDay.TryGetValue(day, out var dayRows);
                    dayRows ??= [];

                    return (object)new
                    {
                        day = day.ToString("dd/MM"),
                        total = dayRows.Length,
                        errors = dayRows.Count(x => x.IsError),
                        avgLatencyMs = dayRows.Length == 0
                            ? 0
                            : Math.Round(dayRows.Average(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero)
                    };
                })
                .ToList();
        }

        private static IReadOnlyList<object> BuildAiActionBreakdown(IReadOnlyCollection<AiLogAnalyticsRow> rows)
        {
            return rows
                .GroupBy(x => x.ActionType)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Take(8)
                .Select(group => (object)new
                {
                    actionType = group.Key,
                    total = group.Count(),
                    errors = group.Count(x => x.IsError),
                    avgLatencyMs = Math.Round(group.Average(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero),
                    maxLatencyMs = Math.Round(group.Max(x => x.ProcessingTime) * 1000d, 0, MidpointRounding.AwayFromZero)
                })
                .ToList();
        }

        private static IReadOnlyList<object> BuildSlowestAiItems(IReadOnlyCollection<AiLogAnalyticsRow> rows)
        {
            return rows
                .OrderByDescending(x => x.ProcessingTime)
                .ThenByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(x => (object)new
                {
                    x.ActionType,
                    x.IsError,
                    x.IsGuest,
                    latencyMs = Math.Round(x.ProcessingTime * 1000d, 0, MidpointRounding.AwayFromZero),
                    x.CreatedAt,
                    user = string.IsNullOrWhiteSpace(x.Username)
                        ? (x.IsGuest ? "Khách" : "Không xác định")
                        : $"{x.Username} • {x.Email}"
                })
                .ToList();
        }

        private static double CalculatePercentileMs(IEnumerable<double> values, double percentile)
        {
            var ordered = values
                .Where(x => x > 0)
                .OrderBy(x => x)
                .ToArray();

            if (ordered.Length == 0)
            {
                return 0;
            }

            var clamped = Math.Clamp(percentile, 0d, 1d);
            var index = (int)Math.Ceiling(clamped * ordered.Length) - 1;
            index = Math.Clamp(index, 0, ordered.Length - 1);
            return Math.Round(ordered[index] * 1000d, 0, MidpointRounding.AwayFromZero);
        }

        private static object BuildAdminProfile(
            User admin,
            AdminProfileMeta profileMeta,
            ProfileStatsSnapshot profileStats)
        {
            return new
            {
                admin.UserId,
                admin.Username,
                admin.FullName,
                admin.Email,
                phone = profileMeta.Phone,
                bio = profileMeta.Bio,
                role = admin.Role.RoleName,
                admin.IsLocked,
                admin.IsEmailVerified,
                admin.CreatedAt,
                totalUploads = profileStats.TotalUploads,
                totalQuizAttempts = profileStats.TotalQuizAttempts,
                averageQuizScore = profileStats.AverageQuizScore,
                activeLearningDays = profileStats.ActiveLearningDays
            };
        }

        private static string BuildProfileMetaSettingKey(int userId)
        {
            return $"user:{userId}:profile-meta";
        }

        private static string BuildUserSettingPrefix(int userId)
        {
            return $"user:{userId}:";
        }

        private async Task TryDispatchNotificationAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch
            {
                // Keep admin flows successful even when downstream notification delivery fails.
            }
        }

        private static string BuildModerationAlertMessage(string? reason, string username)
        {
            var normalizedReason = (reason ?? string.Empty).Trim();
            var actor = string.IsNullOrWhiteSpace(username) ? "một người dùng" : $"@{username}";
            if (normalizedReason.StartsWith(ContentModerationPolicy.PolicyReasonPrefix, StringComparison.Ordinal))
            {
                return $"Nội dung của {actor} bị hệ thống gắn cờ vi phạm chính sách và đang chờ quản trị viên xem xét.";
            }

            return $"Nội dung của {actor} đang nằm trong hàng chờ duyệt và cần quyết định từ quản trị viên.";
        }

        private static string NormalizeAdminAiActionTitle(string actionType)
        {
            var value = (actionType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Tác vụ AI";
            }

            return value switch
            {
                "SummaryText" => "Tóm tắt văn bản",
                "SummaryPdf" => "Tóm tắt PDF",
                "SummaryDocx" => "Tóm tắt DOCX",
                "SummaryWebPage" => "Tóm tắt trang web",
                "SummaryImage" => "Tóm tắt hình ảnh",
                "SummaryVideo" => "Tóm tắt video",
                "GenerateQuiz" => "Sinh quiz",
                _ => value
            };
        }

        private sealed class AdminActivityItem
        {
            public DateTime At { get; set; }
            public string Kind { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Meta { get; set; } = string.Empty;
        }

        private sealed class AiLogAnalyticsRow
        {
            public string ActionType { get; set; } = string.Empty;
            public bool IsError { get; set; }
            public bool IsGuest { get; set; }
            public double ProcessingTime { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        private sealed class AnalyticsWindow
        {
            public AnalyticsWindow(
                DateTime startDateUtc,
                DateTime endDateUtcExclusive,
                string mode,
                int offset,
                int dayCount,
                string label)
            {
                StartDateUtc = startDateUtc;
                EndDateUtcExclusive = endDateUtcExclusive;
                Mode = mode;
                Offset = offset;
                DayCount = dayCount;
                Label = label;
            }

            public DateTime StartDateUtc { get; }
            public DateTime EndDateUtcExclusive { get; }
            public string Mode { get; }
            public int Offset { get; }
            public int DayCount { get; }
            public string Label { get; }
        }

        private sealed class AdminProfileMeta
        {
            public string Phone { get; set; } = string.Empty;
            public string Bio { get; set; } = string.Empty;
        }

        private sealed class ProfileStatsSnapshot
        {
            public int TotalUploads { get; set; }
            public int TotalQuizAttempts { get; set; }
            public double AverageQuizScore { get; set; }
            public int ActiveLearningDays { get; set; }
        }

        private sealed class AdminProfileStatsSnapshot
        {
            public int TotalAuditActions { get; set; }
            public int ManagedUsers { get; set; }
            public int ReviewedContents { get; set; }
            public int CreatedAdmins { get; set; }
            public DateTime? LastAdminActionAt { get; set; }
        }

        private sealed class AdminPremiumState
        {
            public string Status { get; set; } = "none";
            public string Label { get; set; } = "Chưa đăng ký";
            public DateTime? StartedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
        }

        public sealed class AdminUserLockRequest
        {
            public bool IsLocked { get; set; }
            public string? Reason { get; set; }
        }

        public sealed class AdminPremiumSettingsRequest
        {
            public decimal Amount { get; set; }
            public int Days { get; set; } = 30;
        }

        public sealed class AdminPremiumExtendRequest
        {
            public int Days { get; set; } = 30;
            public string? Reason { get; set; }
        }

        public sealed class AdminPremiumCancelRequest
        {
            public string? Reason { get; set; }
        }

        public sealed class AdminPremiumTransactionActionRequest
        {
            public string? Reason { get; set; }
        }

        public sealed class AdminModerationUpdateRequest
        {
            public string Status { get; set; } = string.Empty;
            public string? Reason { get; set; }
        }

        public sealed class AdminUpsertUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = "User";
            public bool IsLocked { get; set; }
            public bool IsEmailVerified { get; set; } = true;
            public string Password { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public sealed class AdminUpdateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = "User";
            public bool IsLocked { get; set; }
            public bool IsEmailVerified { get; set; } = true;
        }

        public sealed class AdminDeleteUserRequest
        {
            public string? Reason { get; set; }
        }

        public sealed class AdminCreateAccountRequest
        {
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
        }
    }
}
