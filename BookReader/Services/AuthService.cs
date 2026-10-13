using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Recuests.Auth;
using BookReader.Responses.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;


namespace BookReader.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public AuthService(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLower();

            if (await _db.Users.AnyAsync(u => u.Email == email))
                throw new AppException(409, "Пользователь с таким email уже существует");

            var user = new User
            {
                Email = email,
                DisplayName = request.DisplayName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = UserRole.User
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return await BuildAuthResponseAsync(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLower();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
                ?? throw new AppException(401, "Неверный email или пароль");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new AppException(401, "Неверный email или пароль");

            return await BuildAuthResponseAsync(user);
        }

        public async Task<AuthResponse> RefreshAsync(string refreshToken)
        {
            var now = DateTime.UtcNow;
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await _db.RefreshTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash
                    && token.RevokedAt == null
                    && token.ExpiresAt > now)
                ?? throw new AppException(401, "Недействительный или истёкший refresh-токен");

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var revokedCount = await _db.RefreshTokens
                    .Where(token => token.Id == storedToken.Id
                        && token.RevokedAt == null
                        && token.ExpiresAt > now)
                    .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now));

                if (revokedCount == 0)
                    throw new AppException(401, "Недействительный или истёкший refresh-токен");

                var response = await BuildAuthResponseAsync(storedToken.User);
                await transaction.CommitAsync();
                return response;
            });
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var now = DateTime.UtcNow;

            await _db.RefreshTokens
                .Where(token => token.TokenHash == tokenHash && token.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now));
        }

        private async Task<AuthResponse> BuildAuthResponseAsync(User user)
        {
            var expiresMinutes = _config.GetValue<int>("Jwt:ExpiresMinutes", 60);
            var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("Jwt:RefreshTokenExpiresDays", 30));
            var refreshToken = GenerateRefreshToken();

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(refreshToken),
                ExpiresAt = refreshTokenExpiresAt
            });
            await _db.SaveChangesAsync();

            return new AuthResponse
            {
                Token = GenerateToken(user, expiresMinutes),
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Role = user.Role.ToString()
                }
            };
        }

        private static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static string HashRefreshToken(string refreshToken)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }

        private string GenerateToken(User user, int expiresMinutes)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
