using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Google.Apis.Auth;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Web_Project.Models;
using Web_Project.Security;
using Web_Project.Services.Otp;

namespace Web_Project.Services.Auth
{
    public class AuthService : IAuthService
    {
        private const string DefaultUserRoleName = "User";

        private readonly AppDbContext _dbContext;
        private readonly IEmailOtpService _emailOtpService;
        private readonly GoogleAuthSettings _googleAuthSettings;
        private readonly JwtSettings _jwtSettings;
        private readonly JwtSigningMaterial _jwtSigningMaterial;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext dbContext,
            IEmailOtpService emailOtpService,
            IOptions<GoogleAuthSettings> googleAuthSettings,
            IOptions<JwtSettings> jwtSettings,
            JwtSigningMaterial jwtSigningMaterial,
            ILogger<AuthService> logger)
        {
            _dbContext = dbContext;
            _emailOtpService = emailOtpService;
            _googleAuthSettings = googleAuthSettings.Value;
            _jwtSettings = jwtSettings.Value;
            _jwtSigningMaterial = jwtSigningMaterial;
            _logger = logger;
        }

        public async Task<LoginServiceResult> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken)
        {
            var identifier = request.EmailOrUsername.Trim();
            var validationErrors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                validationErrors[nameof(LoginRequest.EmailOrUsername)] = ["Email hoặc tên đăng nhập không được để trống."];
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                validationErrors[nameof(LoginRequest.Password)] = ["Mật khẩu không được để trống."];
            }

            if (validationErrors.Count > 0)
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 400,
                    ValidationErrors = validationErrors
                };
            }

            var normalizedEmail = identifier.ToLowerInvariant();
            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(x => x.Role)
                .FirstOrDefaultAsync(
                    x => x.Email == normalizedEmail || x.Username == identifier,
                    cancellationToken);

            if (user is null || !PasswordHashUtility.VerifyPassword(request.Password, user.PasswordHash))
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 401,
                    Message = "Email/tên đăng nhập hoặc mật khẩu không đúng."
                };
            }

            if (user.IsLocked)
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 403,
                    Message = "Tài khoản đã bị khóa."
                };
            }

            if (!user.IsEmailVerified)
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 403,
                    Message = "Email chưa được xác thực. Vui lòng xác thực OTP trước khi đăng nhập."
                };
            }

            if (!TryCreateAccessToken(user, request.RememberMe, out var accessToken, out var expiresAt))
            {
                _logger.LogError("JWT settings are invalid. Unable to issue token for UserId={UserId}", user.UserId);
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Hệ thống xác thực chưa được cấu hình đúng."
                };
            }

            _logger.LogInformation("User logged in successfully UserId={UserId}", user.UserId);

            return new LoginServiceResult
            {
                Success = true,
                StatusCode = 200,
                Response = CreateLoginResponse(user, accessToken, expiresAt)
            };
        }

        public async Task<LoginServiceResult> GoogleLoginAsync(
            GoogleLoginRequest request,
            CancellationToken cancellationToken)
        {
            var validationErrors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                validationErrors[nameof(GoogleLoginRequest.IdToken)] = ["Google ID token không được để trống."];
            }

            if (validationErrors.Count > 0)
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 400,
                    ValidationErrors = validationErrors
                };
            }

            if (string.IsNullOrWhiteSpace(_googleAuthSettings.ClientId))
            {
                _logger.LogError("GoogleAuth:ClientId is not configured.");
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Hệ thống đăng nhập Google chưa được cấu hình đúng."
                };
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    request.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [_googleAuthSettings.ClientId]
                    });
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning(ex, "Invalid Google ID token.");
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 401,
                    Message = "Google token không hợp lệ hoặc đã hết hạn."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while validating Google ID token.");
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Không thể xác thực Google token ở thời điểm hiện tại."
                };
            }

            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 400,
                    Message = "Tài khoản Google không cung cấp email hợp lệ."
                };
            }

            var email = payload.Email.Trim().ToLowerInvariant();
            var fullName = string.IsNullOrWhiteSpace(payload.Name)
                ? email
                : payload.Name.Trim();

            var user = await _dbContext.Users
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

            if (user is null)
            {
                user = await CreateGoogleUserAsync(email, fullName, cancellationToken);
                if (user is null)
                {
                    return new LoginServiceResult
                    {
                        Success = false,
                        StatusCode = 500,
                        Message = "Không thể tạo tài khoản từ Google. Vui lòng thử lại sau."
                    };
                }
            }

            if (user.IsLocked)
            {
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 403,
                    Message = "Tài khoản đã bị khóa."
                };
            }

            if (!user.IsEmailVerified)
            {
                user.IsEmailVerified = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (!TryCreateAccessToken(user, request.RememberMe, out var accessToken, out var expiresAt))
            {
                _logger.LogError("JWT settings are invalid. Unable to issue token for UserId={UserId}", user.UserId);
                return new LoginServiceResult
                {
                    Success = false,
                    StatusCode = 500,
                    Message = "Hệ thống xác thực chưa được cấu hình đúng."
                };
            }

            _logger.LogInformation("User logged in with Google successfully UserId={UserId}", user.UserId);

            return new LoginServiceResult
            {
                Success = true,
                StatusCode = 200,
                Response = CreateLoginResponse(user, accessToken, expiresAt)
            };
        }

        public async Task<RegisterServiceResult> RegisterAsync(
            RegisterRequest request,
            string requestIp,
            CancellationToken cancellationToken)
        {
            var username = request.Username.Trim();
            var fullName = request.FullName.Trim();
            var email = request.Email.Trim().ToLowerInvariant();

            var validationErrors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(username))
            {
                validationErrors[nameof(RegisterRequest.Username)] = ["Username không được để trống."];
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                validationErrors[nameof(RegisterRequest.FullName)] = ["Họ tên không được để trống."];
            }

            if (!request.AcceptTerms)
            {
                validationErrors[nameof(RegisterRequest.AcceptTerms)] = ["Bạn cần đồng ý với điều khoản sử dụng."];
            }

            if (!HasStrongPassword(request.Password))
            {
                validationErrors[nameof(RegisterRequest.Password)] =
                    ["Mật khẩu cần có ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số."];
            }

            var usernameExists = await _dbContext.Users
                .AnyAsync(x => x.Username == username, cancellationToken);

            if (usernameExists)
            {
                validationErrors[nameof(RegisterRequest.Username)] = ["Tên đăng nhập đã tồn tại."];
            }

            var emailExists = await _dbContext.Users
                .AnyAsync(x => x.Email == email, cancellationToken);

            if (emailExists)
            {
                validationErrors[nameof(RegisterRequest.Email)] = ["Email đã được sử dụng."];
            }

            if (validationErrors.Count > 0)
            {
                return new RegisterServiceResult
                {
                    Success = false,
                    ValidationErrors = validationErrors
                };
            }

            var roleId = await EnsureDefaultUserRoleIdAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordHash = PasswordHashUtility.HashPassword(request.Password),
                RoleId = roleId,
                IsLocked = false,
                IsEmailVerified = false,
                CreatedAt = now
            };

            _dbContext.Users.Add(user);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                return new RegisterServiceResult
                {
                    Success = false,
                    IsConflict = true,
                    Message = "Tên đăng nhập hoặc email đã tồn tại."
                };
            }

            _logger.LogInformation("Registered new user UserId={UserId} Username={Username}", user.UserId, user.Username);

            var otpDispatch = await _emailOtpService.IssueRegisterOtpAsync(user, requestIp, cancellationToken);

            return new RegisterServiceResult
            {
                Success = true,
                Response = new RegisterResponse
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    IsEmailVerified = user.IsEmailVerified,
                    OtpDispatched = otpDispatch.Success,
                    OtpExpiresAt = otpDispatch.ExpiresAt,
                    Message = otpDispatch.Success
                        ? "Đăng ký thành công. Vui lòng kiểm tra email để nhập OTP xác thực."
                        : "Đăng ký thành công nhưng chưa gửi được OTP. Vui lòng gửi lại OTP."
                }
            };
        }

        public Task<OtpVerificationResult> VerifyEmailOtpAsync(
            VerifyEmailOtpRequest request,
            CancellationToken cancellationToken)
        {
            return _emailOtpService.VerifyRegisterOtpAsync(
                request.Email,
                request.OtpCode,
                cancellationToken);
        }

        public Task<OtpDispatchResult> ResendEmailOtpAsync(
            ResendEmailOtpRequest request,
            string requestIp,
            CancellationToken cancellationToken)
        {
            return _emailOtpService.ResendRegisterOtpAsync(
                request.Email,
                requestIp,
                cancellationToken);
        }

        private async Task<int> EnsureDefaultUserRoleIdAsync(CancellationToken cancellationToken)
        {
            var existingRole = await _dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RoleName == DefaultUserRoleName, cancellationToken);

            if (existingRole is not null)
            {
                return existingRole.RoleId;
            }

            var role = new Role { RoleName = DefaultUserRoleName };
            _dbContext.Roles.Add(role);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return role.RoleId;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _dbContext.Entry(role).State = EntityState.Detached;

                var fallback = await _dbContext.Roles
                    .AsNoTracking()
                    .FirstAsync(x => x.RoleName == DefaultUserRoleName, cancellationToken);

                return fallback.RoleId;
            }
        }

        private async Task<User?> CreateGoogleUserAsync(
            string email,
            string fullName,
            CancellationToken cancellationToken)
        {
            var roleId = await EnsureDefaultUserRoleIdAsync(cancellationToken);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var username = await GenerateUniqueUsernameAsync(email, cancellationToken);
                var now = DateTime.UtcNow;

                var newUser = new User
                {
                    Username = username,
                    FullName = fullName,
                    Email = email,
                    PasswordHash = PasswordHashUtility.HashPassword($"GOOGLE::{Guid.NewGuid():N}Aa1"),
                    RoleId = roleId,
                    IsLocked = false,
                    IsEmailVerified = true,
                    CreatedAt = now
                };

                _dbContext.Users.Add(newUser);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return await _dbContext.Users
                        .Include(x => x.Role)
                        .FirstOrDefaultAsync(x => x.UserId == newUser.UserId, cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _dbContext.Entry(newUser).State = EntityState.Detached;

                    var existingUser = await _dbContext.Users
                        .Include(x => x.Role)
                        .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

                    if (existingUser is not null)
                    {
                        return existingUser;
                    }
                }
            }

            _logger.LogError("Failed to create Google user after retries for Email={Email}", email);
            return null;
        }

        private async Task<string> GenerateUniqueUsernameAsync(
            string email,
            CancellationToken cancellationToken)
        {
            var localPart = email.Split('@')[0].Trim();
            var sanitized = Regex.Replace(localPart, "[^a-zA-Z0-9._-]", string.Empty);
            var baseUsername = string.IsNullOrWhiteSpace(sanitized) ? "google_user" : sanitized;
            baseUsername = baseUsername.Length > 48 ? baseUsername[..48] : baseUsername;

            var candidate = baseUsername;
            var suffix = 0;
            while (await _dbContext.Users.AnyAsync(x => x.Username == candidate, cancellationToken))
            {
                suffix++;
                var suffixText = suffix.ToString();
                var maxBaseLength = Math.Max(1, 64 - suffixText.Length);
                var trimmedBase = baseUsername.Length > maxBaseLength ? baseUsername[..maxBaseLength] : baseUsername;
                candidate = $"{trimmedBase}{suffixText}";
            }

            return candidate;
        }

        private static bool HasStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return false;
            }

            var hasUpper = Regex.IsMatch(password, "[A-Z]");
            var hasLower = Regex.IsMatch(password, "[a-z]");
            var hasDigit = Regex.IsMatch(password, "[0-9]");

            return hasUpper && hasLower && hasDigit;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

        private bool TryCreateAccessToken(
            User user,
            bool rememberMe,
            out string accessToken,
            out DateTime expiresAt)
        {
            accessToken = string.Empty;
            expiresAt = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            var lifetime = rememberMe
                ? TimeSpan.FromDays(Math.Max(1, _jwtSettings.RememberMeAccessTokenDays))
                : TimeSpan.FromMinutes(Math.Max(1, _jwtSettings.AccessTokenMinutes));
            expiresAt = now.Add(lifetime);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role?.RoleName ?? DefaultUserRoleName)
            };

            var signingCredentials = _jwtSigningMaterial.CreateSigningCredentials();

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: signingCredentials);

            accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            return true;
        }

        private static LoginResponse CreateLoginResponse(User user, string accessToken, DateTime expiresAt)
        {
            return new LoginResponse
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role?.RoleName ?? DefaultUserRoleName,
                AccessToken = accessToken,
                ExpiresAt = expiresAt
            };
        }
    }
}

