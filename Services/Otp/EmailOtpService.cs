using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web_Project.Models;
using Web_Project.Services.Email;

namespace Web_Project.Services.Otp
{
    public class EmailOtpService : IEmailOtpService
    {
        private const string RegisterPurpose = "Register";

        private readonly AppDbContext _dbContext;
        private readonly IEmailSender _emailSender;
        private readonly EmailOtpSettings _otpSettings;
        private readonly ILogger<EmailOtpService> _logger;

        public EmailOtpService(
            AppDbContext dbContext,
            IEmailSender emailSender,
            IOptions<EmailOtpSettings> otpOptions,
            ILogger<EmailOtpService> logger)
        {
            _dbContext = dbContext;
            _emailSender = emailSender;
            _otpSettings = otpOptions.Value;
            _logger = logger;
        }

        public async Task<OtpDispatchResult> IssueRegisterOtpAsync(
            User user,
            string requestIp,
            CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(user.Email);
            var now = DateTime.UtcNow;
            var expireAt = now.AddMinutes(Math.Max(_otpSettings.ExpireMinutes, 1));

            var activeOtps = await _dbContext.EmailVerificationOtps
                .Where(x => x.Email == email && x.Purpose == RegisterPurpose && !x.IsUsed)
                .ToListAsync(cancellationToken);

            foreach (var activeOtp in activeOtps)
            {
                activeOtp.IsUsed = true;
                activeOtp.UsedAt = now;
            }

            var otpCode = GenerateOtpCode();
            var salt = GenerateSalt();
            var hash = HashOtp(otpCode, salt);

            var otpEntity = new EmailVerificationOtp
            {
                Email = email,
                Purpose = RegisterPurpose,
                UserId = user.UserId,
                OtpHash = hash,
                OtpSalt = salt,
                AttemptCount = 0,
                IsUsed = false,
                CreatedAt = now,
                ExpiresAt = expireAt,
                RequestedIp = requestIp
            };

            _dbContext.EmailVerificationOtps.Add(otpEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await SendRegisterOtpMailAsync(email, otpCode, expireAt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send register OTP email to {Email}", email);
                return new OtpDispatchResult
                {
                    Success = false,
                    ExpiresAt = expireAt,
                    Message = "Tạo OTP thành công nhưng gửi email thất bại."
                };
            }

            return new OtpDispatchResult
            {
                Success = true,
                ExpiresAt = expireAt,
                Message = "Đã gửi mã OTP tới email."
            };
        }

        public async Task<OtpVerificationResult> VerifyRegisterOtpAsync(
            string email,
            string otpCode,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = NormalizeEmail(email);
            var now = DateTime.UtcNow;
            var maxAttempts = Math.Max(_otpSettings.MaxAttempts, 1);

            var otpEntity = await _dbContext.EmailVerificationOtps
                .Where(x => x.Email == normalizedEmail && x.Purpose == RegisterPurpose && !x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (otpEntity is null)
            {
                return new OtpVerificationResult
                {
                    Success = false,
                    Message = "Không tìm thấy OTP hợp lệ."
                };
            }

            if (otpEntity.ExpiresAt <= now)
            {
                otpEntity.IsUsed = true;
                otpEntity.UsedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new OtpVerificationResult
                {
                    Success = false,
                    Message = "OTP đã hết hạn."
                };
            }

            if (otpEntity.AttemptCount >= maxAttempts)
            {
                otpEntity.IsUsed = true;
                otpEntity.UsedAt = now;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new OtpVerificationResult
                {
                    Success = false,
                    Message = "OTP đã vượt quá số lần thử cho phép."
                };
            }

            var isMatch = VerifyOtp(otpCode, otpEntity.OtpSalt, otpEntity.OtpHash);
            if (!isMatch)
            {
                otpEntity.AttemptCount += 1;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new OtpVerificationResult
                {
                    Success = false,
                    Message = "OTP không chính xác."
                };
            }

            otpEntity.IsUsed = true;
            otpEntity.UsedAt = now;

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

            if (user is null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return new OtpVerificationResult
                {
                    Success = false,
                    Message = "Không tìm thấy tài khoản cần xác thực."
                };
            }

            user.IsEmailVerified = true;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new OtpVerificationResult
            {
                Success = true,
                Message = "Xác thực email thành công."
            };
        }

        public async Task<OtpDispatchResult> ResendRegisterOtpAsync(
            string email,
            string requestIp,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = NormalizeEmail(email);
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

            if (user is null)
            {
                return new OtpDispatchResult
                {
                    Success = false,
                    Message = "Email chưa đăng ký tài khoản."
                };
            }

            if (user.IsEmailVerified)
            {
                return new OtpDispatchResult
                {
                    Success = false,
                    Message = "Email đã được xác thực."
                };
            }

            return await IssueRegisterOtpAsync(user, requestIp, cancellationToken);
        }

        private async Task SendRegisterOtpMailAsync(
            string email,
            string otpCode,
            DateTime expireAt,
            CancellationToken cancellationToken)
        {
            var subject = "Ma OTP xac thuc tai khoan";
            var textBody =
                $"Ma OTP cua ban la: {otpCode}. Ma co hieu luc den {expireAt:yyyy-MM-dd HH:mm:ss} UTC.";
            var htmlBody =
                $"<p>Ma OTP xac thuc tai khoan cua ban la:</p><h2>{otpCode}</h2><p>Ma co hieu luc den <strong>{expireAt:yyyy-MM-dd HH:mm:ss} UTC</strong>.</p>";

            await _emailSender.SendAsync(
                email,
                subject,
                htmlBody,
                textBody,
                cancellationToken);
        }

        private static string GenerateOtpCode()
        {
            return RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        }

        private static string GenerateSalt()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        }

        private static string HashOtp(string otpCode, string salt)
        {
            var input = Encoding.UTF8.GetBytes($"{otpCode}:{salt}");
            var hash = SHA256.HashData(input);
            return Convert.ToBase64String(hash);
        }

        private static bool VerifyOtp(string otpCode, string salt, string storedHash)
        {
            var computedHash = HashOtp(otpCode, salt);

            var left = Convert.FromBase64String(computedHash);
            var right = Convert.FromBase64String(storedHash);

            return CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
