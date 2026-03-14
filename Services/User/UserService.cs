using Microsoft.EntityFrameworkCore;
using Web_Project.Models;
using Web_Project.Models.Dtos.User;

namespace Web_Project.Services.Users
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;

        public UserService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserProfileServiceResult> GetProfileAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (user is null)
            {
                return new UserProfileServiceResult
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                };
            }

            return new UserProfileServiceResult
            {
                Success = true,
                Response = new ProfileResponse
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    Email = user.Email,
                    IsLocked = user.IsLocked,
                    IsEmailVerified = user.IsEmailVerified,
                    CreatedAt = user.CreatedAt
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

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var fullName = request.FullName.Trim();

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

            await _dbContext.SaveChangesAsync(cancellationToken);

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
                    IsLocked = user.IsLocked,
                    IsEmailVerified = user.IsEmailVerified,
                    CreatedAt = user.CreatedAt
                }
            };
        }
    }
}
