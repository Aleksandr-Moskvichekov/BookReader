using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Responses.Books;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookReader.Controllers;

[ApiController]
[Route("api/collections")]
public class CollectionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CollectionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("top-rated")]
    public Task<ActionResult<BookCollectionResponse>> GetTopRated(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var books = _db.Books.AsNoTracking()
            .OrderBy(book => book.Ratings.Count == 0)
            .ThenByDescending(book => book.Ratings.Select(rating => (double?)rating.Score).Average())
            .ThenByDescending(book => book.Id);

        return GetPageAsync(books, page, pageSize, cancellationToken);
    }

    [HttpGet("popular")]
    public Task<ActionResult<BookCollectionResponse>> GetPopular(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var books = _db.Books.AsNoTracking()
            .OrderByDescending(book => book.ViewsCount)
            .ThenByDescending(book => book.Id);

        return GetPageAsync(books, page, pageSize, cancellationToken);
    }

    [HttpGet("new")]
    public Task<ActionResult<BookCollectionResponse>> GetNew(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var books = _db.Books.AsNoTracking()
            .OrderByDescending(book => book.CreatedAt)
            .ThenByDescending(book => book.Id);

        return GetPageAsync(books, page, pageSize, cancellationToken);
    }

    private async Task<ActionResult<BookCollectionResponse>> GetPageAsync(
        IOrderedQueryable<Book> books,
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
        var items = await books
            .Skip((int)offset)
            .Take(pageSize)
            .Select(book => new CollectionBookResponse
            {
                Id = book.Id,
                Title = book.Title,
                PublishYear = book.PublishYear,
                Author = book.Author.DisplayName,
                Genres = book.Genres.Select(genre => genre.Name).ToList(),
                AverageRating = book.Ratings.Select(rating => (double?)rating.Score).Average(),
                ViewsCount = book.ViewsCount,
                CreatedAt = book.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new BookCollectionResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}
