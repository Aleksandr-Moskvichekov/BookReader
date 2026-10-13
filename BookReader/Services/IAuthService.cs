using BookReader.Recuests.Auth;
using BookReader.Responses.Auth;

namespace BookReader.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshAsync(string refreshToken);
    }
}
