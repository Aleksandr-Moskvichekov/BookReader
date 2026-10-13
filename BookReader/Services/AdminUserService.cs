using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace BookReader.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly ApplicationDbContext _db;

        public AdminUserService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<AdminUserListResponse> GetUsersAsync(
            string? search,
            string? role,
            bool? isBanned,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
                throw new AppException(400, "Параметры пагинации должны быть: page >= 1, pageSize от 1 до 100");

            var query = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(user => EF.Functions.ILike(user.Email, pattern)
                    || EF.Functions.ILike(user.DisplayName, pattern));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                if (!Enum.TryParse<UserRole>(role, true, out var parsedRole)
                    || !Enum.IsDefined(typeof(UserRole), parsedRole))
                    throw new AppException(400, "Неизвестная роль пользователя");

                query = query.Where(user => user.Role == parsedRole);
            }

            if (isBanned.HasValue)
                query = query.Where(user => user.IsBanned == isBanned.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
            var users = await query
                .OrderBy(user => user.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(user => ToResponse(user))
                .ToListAsync(cancellationToken);

            return new AdminUserListResponse
            {
                Users = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<AdminUserResponse> GetUserAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
                ?? throw new AppException(404, "Пользователь не найден");

            return ToResponse(user);
        }

        public async Task<AdminUserResponse> ChangeRoleAsync(
            int id,
            string role,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<UserRole>(role, true, out var newRole)
                || !Enum.IsDefined(typeof(UserRole), newRole))
                throw new AppException(400, "Неизвестная роль пользователя");

            var user = await _db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
                ?? throw new AppException(404, "Пользователь не найден");

            if (user.Id == adminUserId && newRole != UserRole.Admin)
                throw new AppException(403, "Нельзя изменить собственную роль администратора");

            if (user.Role != newRole)
            {
                user.Role = newRole;
                user.TokenVersion++;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return ToResponse(user);
        }

        public async Task<AdminUserResponse> BanAsync(
            int id,
            string reason,
            int adminUserId,
            CancellationToken cancellationToken = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
                ?? throw new AppException(404, "Пользователь не найден");

            if (user.Id == adminUserId)
                throw new AppException(403, "Нельзя забанить самого себя");

            if (user.IsBootstrapAdmin)
                throw new AppException(403, "Нельзя забанить начального администратора");

            var trimmedReason = reason.Trim();
            if (trimmedReason.Length == 0)
                throw new AppException(400, "Укажите причину бана");

            var now = DateTime.UtcNow;
            user.IsBanned = true;
            user.BanReason = trimmedReason;
            user.BannedAt = now;
            user.TokenVersion++;

            var activeRefreshTokens = await _db.RefreshTokens
                .Where(token => token.UserId == id && token.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeRefreshTokens)
                token.RevokedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
            return ToResponse(user);
        }

        public async Task<AdminUserResponse> UnbanAsync(int id, CancellationToken cancellationToken = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
                ?? throw new AppException(404, "Пользователь не найден");

            if (!user.IsBanned)
                return ToResponse(user);

            user.IsBanned = false;
            user.BanReason = null;
            user.BannedAt = null;
            await _db.SaveChangesAsync(cancellationToken);

            return ToResponse(user);
        }

        private static AdminUserResponse ToResponse(User user)
        {
            return new AdminUserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role.ToString(),
                IsBanned = user.IsBanned,
                BanReason = user.BanReason,
                BannedAt = user.BannedAt,
                IsBootstrapAdmin = user.IsBootstrapAdmin,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
