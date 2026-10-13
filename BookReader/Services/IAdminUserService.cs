using BookReader.Responses.Admin;

namespace BookReader.Services
{
    public interface IAdminUserService
    {
        Task<AdminUserListResponse> GetUsersAsync(
            string? search,
            string? role,
            bool? isBanned,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<AdminUserResponse> GetUserAsync(int id, CancellationToken cancellationToken = default);
        Task<AdminUserResponse> ChangeRoleAsync(
            int id,
            string role,
            int adminUserId,
            CancellationToken cancellationToken = default);
        Task<AdminUserResponse> BanAsync(
            int id,
            string reason,
            int adminUserId,
            CancellationToken cancellationToken = default);
        Task<AdminUserResponse> UnbanAsync(int id, CancellationToken cancellationToken = default);
    }
}
