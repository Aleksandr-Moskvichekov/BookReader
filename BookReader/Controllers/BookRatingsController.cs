using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Requests.Books;
using BookReader.Responses.Books;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookReader.Controllers;

[ApiController]
[Route("api/books/{bookId:int}/rating")]
public class BookRatingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public BookRatingsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Возвращает средний рейтинг и количество оценок книги.</summary>
    /// <remarks>Доступно гостям. Для авторизованного пользователя дополнительно возвращается его оценка; если оценок нет, averageRating равен null.</remarks>
    [HttpGet]
    public async Task<ActionResult<BookRatingResponse>> GetRating(int bookId, CancellationToken cancellationToken)
    {
        var summary = await _db.Books.AsNoTracking()
            .Where(book => book.Id == bookId)
            .Select(book => new
            {
                AverageRating = book.Ratings.Select(rating => (double?)rating.Score).Average(),
                RatingCount = book.Ratings.Count
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (summary is null)
            return NotFound();

        int? myRating = null;
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            myRating = await _db.Ratings.AsNoTracking()
                .Where(rating => rating.BookId == bookId && rating.UserId == userId.Value)
                .Select(rating => (int?)rating.Score)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return Ok(new BookRatingResponse
        {
            AverageRating = summary.AverageRating,
            RatingCount = summary.RatingCount,
            MyRating = myRating
        });
    }

    /// <summary>Ставит или обновляет свою оценку книги.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin. Score должен быть от 1 до 5.</remarks>
    [HttpPut]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> SetRating(int bookId, RateBookRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();
        if (!await _db.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
            return NotFound();

        var rating = await _db.Ratings.SingleOrDefaultAsync(
            item => item.BookId == bookId && item.UserId == userId.Value, cancellationToken);
        if (rating is null)
        {
            _db.Ratings.Add(new Rating
            {
                BookId = bookId,
                UserId = userId.Value,
                Score = request.Score
            });
        }
        else
        {
            rating.Score = request.Score;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Удаляет свою оценку книги.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin.</remarks>
    [HttpDelete]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> DeleteRating(int bookId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();
        if (!await _db.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
            return NotFound();

        await _db.Ratings
            .Where(rating => rating.BookId == bookId && rating.UserId == userId.Value)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;
}
