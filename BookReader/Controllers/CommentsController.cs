using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Requests.Comments;
using BookReader.Responses.Comments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookReader.Controllers;

[ApiController]
[Route("api")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CommentsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Возвращает комментарии к книге, начиная с самых новых.</summary>
    /// <remarks>Доступно гостям. Поддерживаются page и pageSize (максимум 100).</remarks>
    [HttpGet("books/{bookId:int}/comments")]
    public async Task<ActionResult<CommentListResponse>> GetComments(
        int bookId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1)
            return BadRequest("page и pageSize должны быть положительными числами");

        pageSize = Math.Min(pageSize, 100);
        var offset = (long)(page - 1) * pageSize;
        if (offset > int.MaxValue)
            return BadRequest("Номер страницы слишком велик");

        if (!await _db.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
            return NotFound();

        var comments = _db.Comments.AsNoTracking().Where(comment => comment.BookId == bookId);
        var totalCount = await comments.CountAsync(cancellationToken);
        var items = await comments
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(comment => new CommentResponse
            {
                Id = comment.Id,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                UserId = comment.UserId,
                DisplayName = comment.User.DisplayName
            })
            .ToListAsync(cancellationToken);

        return Ok(new CommentListResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    /// <summary>Добавляет комментарий к книге от имени текущего пользователя.</summary>
    /// <remarks>Требуется авторизация с ролью User, Author или Admin. Текст комментария не может быть пустым.</remarks>
    [HttpPost("books/{bookId:int}/comments")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> AddComment(
        int bookId,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized();

        if (!await _db.Books.AnyAsync(book => book.Id == bookId, cancellationToken))
            return NotFound();

        var text = request.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Текст комментария не может быть пустым");

        var comment = new Comment
        {
            BookId = bookId,
            UserId = userId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new CommentResponse
        {
            Id = comment.Id,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt,
            UserId = userId,
            DisplayName = User.Identity?.Name ?? string.Empty
        };
        return CreatedAtAction(nameof(GetComments), new { bookId }, response);
    }

    /// <summary>Удаляет комментарий.</summary>
    /// <remarks>Пользователь может удалить свой комментарий; Admin может удалить любой комментарий.</remarks>
    [HttpDelete("comments/{commentId:int}")]
    [Authorize(Policy = "User")]
    public async Task<IActionResult> DeleteComment(int commentId, CancellationToken cancellationToken)
    {
        var comment = await _db.Comments.SingleOrDefaultAsync(item => item.Id == commentId, cancellationToken);
        if (comment is null)
            return NotFound();

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole(nameof(UserRole.Admin));
        if (!isAdmin && (!int.TryParse(userIdValue, out var userId) || comment.UserId != userId))
            return Forbid();

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
