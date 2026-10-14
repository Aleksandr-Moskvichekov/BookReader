using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Requests.Users;
using BookReader.Responses.Books;
using BookReader.Responses.Users;
using BookReader.Services;
using BookReader.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookReader.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ApplicationDbContext db, IFileStorageService storage, ILogger<UsersController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Возвращает публичный профиль пользователя.</summary>
    /// <remarks>Содержит отображаемое имя, дату регистрации и временную ссылку на аватар, если он задан. Доступно гостям.</remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PublicUserProfileResponse>> GetProfile(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.DisplayName, item.AvatarKey, item.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return NotFound();

        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(user.AvatarKey))
        {
            try
            {
                avatarUrl = await _storage.GetPresignedUrlAsync(user.AvatarKey);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Не удалось получить ссылку на аватар пользователя {UserId}", id);
            }
        }

        return Ok(new PublicUserProfileResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            AvatarUrl = avatarUrl,
            CreatedAt = user.CreatedAt
        });
    }

    /// <summary>Изменяет отображаемое имя текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin.</remarks>
    [HttpPut("me")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Отображаемое имя обязательно");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        user.DisplayName = request.DisplayName.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Изменяет пароль текущего пользователя.</summary>
    /// <remarks>Требуется текущий пароль. После успешной смены отзываются refresh-токены и ранее выданные access-токены.</remarks>
    [HttpPut("me/password")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Текущий пароль указан неверно");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.TokenVersion++;
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Загружает или заменяет аватар текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin. Multipart-поле file должно содержать изображение JPG, PNG или WebP с соответствующим MIME-типом.</remarks>
    [HttpPut("me/avatar")]
    [Authorize(Policy = "User")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Файл аватара не указан или пустой");

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();

        var contentType = UploadedBookFormatValidator.GetCoverContentType(file.FileName);
        using (var validationStream = file.OpenReadStream())
        {
            if (!UploadedBookFormatValidator.IsValidCover(file.FileName, file.ContentType, validationStream))
                return BadRequest("Допустимы только корректные изображения JPG, PNG или WebP с соответствующим MIME-типом");
        }

        string newAvatarKey;
        try
        {
            await using var uploadStream = file.OpenReadStream();
            newAvatarKey = await _storage.UploadAsync(uploadStream, file.FileName, contentType!, "avatars");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить аватар пользователя {UserId}", user.Id);
            return Problem(title: "Не удалось загрузить аватар", statusCode: StatusCodes.Status502BadGateway);
        }

        var oldAvatarKey = user.AvatarKey;
        user.AvatarKey = newAvatarKey;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(newAvatarKey);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Не удалось удалить незарегистрированный аватар {StorageKey}", newAvatarKey);
            }

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldAvatarKey))
        {
            try
            {
                await _storage.DeleteAsync(oldAvatarKey);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Старый аватар {StorageKey} пользователя {UserId} не удалён из S3", oldAvatarKey, user.Id);
            }
        }

        return NoContent();
    }

    /// <summary>Удаляет аватар текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin.</remarks>
    [HttpDelete("me/avatar")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(user.AvatarKey))
            return NoContent();

        var avatarKey = user.AvatarKey;
        user.AvatarKey = null;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _storage.DeleteAsync(avatarKey);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Аватар {StorageKey} пользователя {UserId} не удалён из S3", avatarKey, user.Id);
        }

        return NoContent();
    }

    /// <summary>Возвращает страницу избранных книг текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin. Поддерживаются page и pageSize (максимум 100).</remarks>
    [HttpGet("me/favorites")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<UserBookListResponse>> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var books = _db.UserFavorites.AsNoTracking()
            .Where(favorite => favorite.UserId == userId.Value)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenByDescending(favorite => favorite.BookId)
            .Select(favorite => new UserBookResponse
            {
                Id = favorite.Book.Id,
                Title = favorite.Book.Title,
                PublishYear = favorite.Book.PublishYear,
                Author = favorite.Book.Author.DisplayName,
                Genres = favorite.Book.Genres.Select(genre => genre.Name).ToList(),
                CreatedAt = favorite.Book.CreatedAt,
                AddedToFavoritesAt = favorite.CreatedAt
            });

        return await GetBookPageAsync(books, page, pageSize, cancellationToken);
    }

    /// <summary>Добавляет книгу в избранное текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin. Операция идемпотентна: повторный запрос не создаёт дубликат.</remarks>
    [HttpPut("me/favorites/{bookId:int}")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> AddFavorite(int bookId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();
        if (!await _db.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
            return NotFound();
        if (await _db.UserFavorites.AnyAsync(
                favorite => favorite.UserId == userId.Value && favorite.BookId == bookId, cancellationToken))
            return NoContent();

        _db.UserFavorites.Add(new UserFavorite
        {
            UserId = userId.Value,
            BookId = bookId
        });
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Удаляет книгу из избранного текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin.</remarks>
    [HttpDelete("me/favorites/{bookId:int}")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> RemoveFavorite(int bookId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        await _db.UserFavorites
            .Where(favorite => favorite.UserId == userId.Value && favorite.BookId == bookId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Возвращает страницу истории книг текущего пользователя.</summary>
    /// <remarks>История содержит время последнего просмотра страницы книги или успешного открытия её файла. Поддерживаются page и pageSize (максимум 100).</remarks>
    [HttpGet("me/history")]
    [Authorize(Policy = "User")]
    public async Task<ActionResult<UserBookListResponse>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var books = _db.UserBookHistories.AsNoTracking()
            .Where(history => history.UserId == userId.Value)
            .OrderByDescending(history => history.LastAccessedAt)
            .ThenByDescending(history => history.BookId)
            .Select(history => new UserBookResponse
            {
                Id = history.Book.Id,
                Title = history.Book.Title,
                PublishYear = history.Book.PublishYear,
                Author = history.Book.Author.DisplayName,
                Genres = history.Book.Genres.Select(genre => genre.Name).ToList(),
                CreatedAt = history.Book.CreatedAt,
                LastAccessedAt = history.LastAccessedAt
            });

        return await GetBookPageAsync(books, page, pageSize, cancellationToken);
    }

    /// <summary>Возвращает книги, загруженные текущим автором.</summary>
    /// <remarks>Требуется роль Author или Admin. Поддерживаются page и pageSize (максимум 100).</remarks>
    [HttpGet("me/books")]
    [Authorize(Policy = "Author")]
    public async Task<ActionResult<UserBookListResponse>> GetMyBooks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var books = _db.Books.AsNoTracking()
            .Where(book => book.AuthorId == userId.Value)
            .OrderByDescending(book => book.CreatedAt)
            .ThenByDescending(book => book.Id)
            .Select(book => new UserBookResponse
            {
                Id = book.Id,
                Title = book.Title,
                PublishYear = book.PublishYear,
                Author = book.Author.DisplayName,
                Genres = book.Genres.Select(genre => genre.Name).ToList(),
                CreatedAt = book.CreatedAt
            });

        return await GetBookPageAsync(books, page, pageSize, cancellationToken);
    }

    private async Task<ActionResult<UserBookListResponse>> GetBookPageAsync(
        IQueryable<UserBookResponse> books,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest("page и pageSize должны быть положительными числами");

        pageSize = Math.Min(pageSize, 100);
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
            return BadRequest("Номер страницы слишком велик");

        var totalCount = await books.CountAsync(cancellationToken);
        var items = await books.Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new UserBookListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;

    private async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        return userId is null
            ? null
            : await _db.Users.SingleOrDefaultAsync(user => user.Id == userId.Value, cancellationToken);
    }
}
