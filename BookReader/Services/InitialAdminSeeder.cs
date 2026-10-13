using System.ComponentModel.DataAnnotations;
using BookReader.Data;
using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BookReader.Services
{
    public static class InitialAdminSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            var email = configuration["InitialAdmin:Email"]?.Trim().ToLowerInvariant();
            var displayName = configuration["InitialAdmin:DisplayName"]?.Trim();
            var password = configuration["InitialAdmin:Password"];

            if (!string.IsNullOrWhiteSpace(email))
            {
                var configuredAdmin = await db.Users.FirstOrDefaultAsync(
                    user => user.Role == UserRole.Admin && user.Email == email,
                    cancellationToken);
                if (configuredAdmin is not null && !configuredAdmin.IsBootstrapAdmin)
                {
                    configuredAdmin.IsBootstrapAdmin = true;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            if (await db.Users.AnyAsync(user => user.Role == UserRole.Admin, cancellationToken))
                return;

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Начальный администратор не создан: заполните InitialAdmin в appsettings.Admin.json");
                return;
            }

            if (!new EmailAddressAttribute().IsValid(email) || password.Length < 6)
                throw new InvalidOperationException("Некорректные данные InitialAdmin: проверьте Email и пароль (минимум 6 символов)");

            if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
                throw new InvalidOperationException("Пользователь с Email начального администратора уже существует, но не имеет роли Admin");

            db.Users.Add(new User
            {
                Email = email,
                DisplayName = displayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
                IsBootstrapAdmin = true
            });

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Создан начальный администратор {Email}", email);
        }
    }
}
