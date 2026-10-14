using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Requests.Books;
using BookReader.Responses.Books;
using BookReader.Services;
using BookReader.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookReader.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController : ControllerBase
{
    private const long MaxBookFileSize = 50L * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<BooksController> _logger;

    public BooksController(ApplicationDbContext db, IFileStorageService storage, ILogger<BooksController> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<BookCatalogResponse>> GetCatalog(
        [FromQuery] string? q,
        [FromQuery] int[]? genreIds,
        [FromQuery] int? year,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDirection = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || year is < 1 or > 9999 || yearFrom < 1 || yearFrom > 9999 || yearTo < 1 || yearTo > 9999
            || yearFrom.HasValue && yearTo.HasValue && yearFrom > yearTo)
            return BadRequest("Некорректные параметры фильтрации или пагинации");

        pageSize = Math.Min(pageSize, 100);
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
            return BadRequest("Номер страницы слишком велик");
        var books = _db.Books.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            books = books.Where(book => book.Title.ToLower().Contains(search)
                || book.Author.DisplayName.ToLower().Contains(search));
        }

        if (genreIds is { Length: > 0 })
        {
            var distinctGenreIds = genreIds.Distinct().ToArray();
            books = books.Where(book => book.Genres.Any(genre => distinctGenreIds.Contains(genre.Id)));
        }

        if (year.HasValue)
            books = books.Where(book => book.PublishYear == year.Value);
        if (yearFrom.HasValue)
            books = books.Where(book => book.PublishYear >= yearFrom.Value);
        if (yearTo.HasValue)
            books = books.Where(book => book.PublishYear <= yearTo.Value);

        var descending = sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        if (!descending && !sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("sortDirection должен быть asc или desc");

        var normalizedSort = sortBy.ToLowerInvariant();
        if (normalizedSort is not ("title" or "year" or "createdat"))
            return BadRequest("sortBy должен быть title, year или createdAt");

        books = normalizedSort switch
        {
            "title" => descending ? books.OrderByDescending(book => book.Title) : books.OrderBy(book => book.Title),
            "year" => descending ? books.OrderByDescending(book => book.PublishYear) : books.OrderBy(book => book.PublishYear),
            "createdat" => descending ? books.OrderByDescending(book => book.CreatedAt) : books.OrderBy(book => book.CreatedAt),
            _ => books
        };

        var totalCount = await books.CountAsync(cancellationToken);
        var items = await books
            .Skip((int)offset)
            .Take(pageSize)
            .Select(book => new BookSummaryResponse
            {
                Id = book.Id,
                Title = book.Title,
                PublishYear = book.PublishYear,
                Author = book.Author.DisplayName,
                Genres = book.Genres.Select(genre => genre.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(new BookCatalogResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookDetailsResponse>> GetBook(int id, CancellationToken cancellationToken)
    {
        var viewUpdateCount = await _db.Books
            .Where(book => book.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(book => book.ViewsCount, book => book.ViewsCount + 1), cancellationToken);
        if (viewUpdateCount == 0)
            return NotFound();

        var book = await _db.Books.AsNoTracking()
            .Include(item => item.Author)
            .Include(item => item.Genres)
            .Include(item => item.Files)
            .Include(item => item.Ratings)
            .SingleAsync(item => item.Id == id, cancellationToken);

        string? coverUrl = null;
        if (!string.IsNullOrWhiteSpace(book.CoverKey))
            coverUrl = await _storage.GetPresignedUrlAsync(book.CoverKey);

        var viewerId = GetCurrentUserId();
        if (viewerId.HasValue)
            await RecordBookAccessAsync(viewerId.Value, id, cancellationToken);

        return Ok(new BookDetailsResponse
        {
            Id = book.Id,
            Title = book.Title,
            PublishYear = book.PublishYear,
            Author = book.Author.DisplayName,
            Genres = book.Genres.Select(genre => genre.Name).ToList(),
            Description = book.Description,
            CoverUrl = coverUrl,
            ViewsCount = book.ViewsCount,
            AverageRating = book.Ratings.Count == 0 ? null : book.Ratings.Average(rating => rating.Score),
            RatingCount = book.Ratings.Count,
            Files = book.Files.Select(file => new BookFileResponse
            {
                Id = file.Id,
                Format = file.Format,
                SizeBytes = file.SizeBytes
            }).ToList()
        });
    }

    [HttpPost]
    [Authorize(Policy = "Author")]
    public async Task<IActionResult> CreateBook(BookWriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Название книги обязательно");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var genres = await LoadGenresAsync(request.GenreIds, cancellationToken);
        if (genres is null)
            return BadRequest("Один или несколько жанров не найдены");

        var book = new Book
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            PublishYear = request.PublishYear,
            AuthorId = userId.Value,
            Genres = genres
        };

        _db.Books.Add(book);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, new { id = book.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Author")]
    public async Task<IActionResult> UpdateBook(int id, BookWriteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Название книги обязательно");

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var book = await _db.Books.Include(item => item.Genres)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();
        if (book.AuthorId != userId.Value)
            return Forbid();

        var genres = await LoadGenresAsync(request.GenreIds, cancellationToken);
        if (genres is null)
            return BadRequest("Один или несколько жанров не найдены");

        book.Title = request.Title.Trim();
        book.Description = request.Description;
        book.PublishYear = request.PublishYear;
        book.Genres = genres;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Author")]
    public async Task<IActionResult> DeleteBook(int id, CancellationToken cancellationToken)
    {
        var book = await _db.Books.Include(item => item.Files)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();

        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole(nameof(UserRole.Admin));
        if (!isAdmin && (userId is null || book.AuthorId != userId.Value))
            return Forbid();

        var storageKeys = book.Files.Select(file => file.StorageKey).ToList();
        if (!string.IsNullOrWhiteSpace(book.CoverKey))
            storageKeys.Add(book.CoverKey);

        try
        {
            foreach (var key in storageKeys.Distinct())
                await _storage.DeleteAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось удалить файлы книги {BookId} из S3", id);
            return Problem(title: "Не удалось удалить файлы книги", statusCode: StatusCodes.Status502BadGateway);
        }

        _db.Books.Remove(book);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/cover")]
    [Authorize(Policy = "Author")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCover(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Файл обложки не указан или пустой");

        var book = await _db.Books.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (userId is null || book.AuthorId != userId.Value)
            return Forbid();

        var contentType = UploadedBookFormatValidator.GetCoverContentType(file.FileName);
        using (var validationStream = file.OpenReadStream())
        {
            if (!UploadedBookFormatValidator.IsValidCover(file.FileName, file.ContentType, validationStream))
                return BadRequest("Допустимы только корректные изображения JPG, PNG или WebP с соответствующим MIME-типом");
        }

        string newCoverKey;
        try
        {
            await using var uploadStream = file.OpenReadStream();
            newCoverKey = await _storage.UploadAsync(uploadStream, file.FileName, contentType!, "covers");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить обложку книги {BookId} в S3", id);
            return Problem(title: "Не удалось загрузить обложку", statusCode: StatusCodes.Status502BadGateway);
        }

        var oldCoverKey = book.CoverKey;
        book.CoverKey = newCoverKey;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(newCoverKey);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Не удалось удалить неиспользуемую обложку {StorageKey}", newCoverKey);
            }

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldCoverKey))
        {
            try
            {
                await _storage.DeleteAsync(oldCoverKey);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Старая обложка {StorageKey} книги {BookId} не удалена из S3", oldCoverKey, id);
            }
        }

        return NoContent();
    }

    [HttpDelete("{id:int}/cover")]
    [Authorize(Policy = "Author")]
    public async Task<IActionResult> DeleteCover(int id, CancellationToken cancellationToken)
    {
        var book = await _db.Books.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (userId is null || book.AuthorId != userId.Value)
            return Forbid();
        if (string.IsNullOrWhiteSpace(book.CoverKey))
            return NoContent();

        var coverKey = book.CoverKey;
        book.CoverKey = null;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _storage.DeleteAsync(coverKey);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Обложка {StorageKey} книги {BookId} не удалена из S3", coverKey, id);
        }

        return NoContent();
    }

    [HttpGet("{id:int}/files")]
    public async Task<ActionResult<IReadOnlyList<BookFileResponse>>> GetFiles(int id, CancellationToken cancellationToken)
    {
        if (!await _db.Books.AnyAsync(book => book.Id == id, cancellationToken))
            return NotFound();

        var files = await _db.BookFiles.AsNoTracking()
            .Where(file => file.BookId == id)
            .OrderBy(file => file.Format)
            .Select(file => new BookFileResponse
            {
                Id = file.Id,
                Format = file.Format,
                SizeBytes = file.SizeBytes
            })
            .ToListAsync(cancellationToken);

        return Ok(files);
    }

    [HttpPost("{id:int}/files")]
    [Authorize(Policy = "Author")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBookFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBookFileSize)]
    public async Task<IActionResult> UploadBookFile(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Файл книги не указан или пустой");
        if (file.Length > MaxBookFileSize)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "Максимальный размер файла — 50 МиБ");

        var book = await _db.Books.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (userId is null || book.AuthorId != userId.Value)
            return Forbid();

        using (var validationStream = file.OpenReadStream())
        {
            if (!UploadedBookFormatValidator.IsValid(file.FileName, file.ContentType, validationStream))
                return BadRequest("Допустимы только EPUB, FB2, PDF и TXT с соответствующим MIME-типом и содержимым");
        }

        var extension = Path.GetExtension(file.FileName).TrimStart('.');
        if (!Enum.TryParse<BookFormat>(extension, ignoreCase: true, out var format))
            return BadRequest("Неизвестный формат книги");

        string storageKey;
        try
        {
            await using var uploadStream = file.OpenReadStream();
            storageKey = await _storage.UploadAsync(
                uploadStream, file.FileName, UploadedBookFormatValidator.GetContentType(file.FileName));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить файл книги {BookId} в S3", id);
            return Problem(title: "Не удалось загрузить файл книги", statusCode: StatusCodes.Status502BadGateway);
        }

        var bookFile = new BookFile
        {
            BookId = id,
            Format = format,
            SizeBytes = file.Length,
            StorageKey = storageKey
        };
        _db.BookFiles.Add(bookFile);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(storageKey);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Не удалось удалить неиспользуемый файл {StorageKey}", storageKey);
            }

            throw;
        }

        return CreatedAtAction(nameof(GetFiles), new { id }, new BookFileResponse
        {
            Id = bookFile.Id,
            Format = bookFile.Format,
            SizeBytes = bookFile.SizeBytes
        });
    }

    [HttpDelete("{id:int}/files/{fileId:int}")]
    [Authorize(Policy = "Author")]
    public async Task<IActionResult> DeleteBookFile(int id, int fileId, CancellationToken cancellationToken)
    {
        var book = await _db.Books.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (userId is null || book.AuthorId != userId.Value)
            return Forbid();

        var file = await _db.BookFiles.SingleOrDefaultAsync(
            item => item.Id == fileId && item.BookId == id, cancellationToken);
        if (file is null)
            return NotFound();

        try
        {
            await _storage.DeleteAsync(file.StorageKey);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось удалить файл {FileId} книги {BookId} из S3", fileId, id);
            return Problem(title: "Не удалось удалить файл книги", statusCode: StatusCodes.Status502BadGateway);
        }

        _db.BookFiles.Remove(file);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/files/{fileId:int}/download-url")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> GetBookFileDownloadUrl(int id, int fileId, CancellationToken cancellationToken)
    {
        var file = await _db.BookFiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == fileId && item.BookId == id, cancellationToken);
        if (file is null)
            return NotFound();

        try
        {
            var url = await _storage.GetPresignedUrlAsync(file.StorageKey, expiryMinutes: 15);
            return Ok(new { url, expiresInMinutes = 15 });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось создать ссылку для скачивания файла {FileId} книги {BookId}", fileId, id);
            return Problem(title: "Не удалось подготовить файл к скачиванию", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("{id:int}/files/{fileId:int}/content")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> GetBookFileContent(int id, int fileId, CancellationToken cancellationToken)
    {
        var file = await _db.BookFiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == fileId && item.BookId == id, cancellationToken);
        if (file is null)
            return NotFound();

        byte[] content;
        string contentType;
        try
        {
            (content, contentType) = await _storage.DownloadAsync(file.StorageKey);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось прочитать файл {FileId} книги {BookId} из S3", fileId, id);
            return Problem(title: "Не удалось получить содержимое файла", statusCode: StatusCodes.Status502BadGateway);
        }

        var readerId = GetCurrentUserId();
        if (readerId.HasValue)
            await RecordBookAccessAsync(readerId.Value, id, cancellationToken);

        var contentStream = new MemoryStream(content, writable: false);
        return File(contentStream, contentType, enableRangeProcessing: true);
    }

    private async Task RecordBookAccessAsync(int userId, int bookId, CancellationToken cancellationToken)
    {
        var history = await _db.UserBookHistories.FindAsync(
            new object[] { userId, bookId }, cancellationToken);
        if (history is null)
        {
            _db.UserBookHistories.Add(new UserBookHistory
            {
                UserId = userId,
                BookId = bookId,
                LastAccessedAt = DateTime.UtcNow
            });
        }
        else
        {
            history.LastAccessedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;

    private async Task<List<Genre>?> LoadGenresAsync(IEnumerable<int> genreIds, CancellationToken cancellationToken)
    {
        var ids = genreIds.Distinct().ToArray();
        var genres = await _db.Genres.Where(genre => ids.Contains(genre.Id)).ToListAsync(cancellationToken);
        return genres.Count == ids.Length ? genres : null;
    }
}
