using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Recuests.Auth;
using BookReader.Responses.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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

            return BuildAuthResponse(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var email = request.Email.Trim().ToLower();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
                ?? throw new AppException(401, "Неверный email или пароль");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new AppException(401, "Неверный email или пароль");

            return BuildAuthResponse(user);
        }

        private AuthResponse BuildAuthResponse(User user)
        {
            var expiresMinutes = _config.GetValue<int>("Jwt:ExpiresMinutes", 60);

            return new AuthResponse
            {
                Token = GenerateToken(user, expiresMinutes),
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes),
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Role = user.Role.ToString()
                }
            };
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
