using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Requests.Genres;
using BookReader.Responses.Genres;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookReader.Controllers;

[ApiController]
[Route("api/genres")]
public class GenresController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GenresController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GenreResponse>>> GetGenres(CancellationToken cancellationToken)
    {
        var genres = await _db.Genres.AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new GenreResponse
            {
                Id = genre.Id,
                Name = genre.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(genres);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GenreResponse>> GetGenre(int id, CancellationToken cancellationToken)
    {
        var genre = await _db.Genres.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new GenreResponse
            {
                Id = item.Id,
                Name = item.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        return genre is null ? NotFound() : Ok(genre);
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CreateGenre(GenreWriteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Название жанра обязательно");
        if (await _db.Genres.AnyAsync(genre => genre.Name.ToLower() == name.ToLower(), cancellationToken))
            return Conflict("Жанр с таким названием уже существует");

        var genre = new Genre { Name = name };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetGenre), new { id = genre.Id }, new GenreResponse
        {
            Id = genre.Id,
            Name = genre.Name
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> RenameGenre(int id, GenreWriteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Название жанра обязательно");

        var genre = await _db.Genres.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (genre is null)
            return NotFound();
        if (await _db.Genres.AnyAsync(
                item => item.Id != id && item.Name.ToLower() == name.ToLower(), cancellationToken))
            return Conflict("Жанр с таким названием уже существует");

        genre.Name = name;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteGenre(int id, CancellationToken cancellationToken)
    {
        var genre = await _db.Genres.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (genre is null)
            return NotFound();

        _db.Genres.Remove(genre);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
