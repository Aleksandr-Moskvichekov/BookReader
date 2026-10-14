using BookReader.Data;
using BookReader.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BookReader.Services;

public static class InitialGenreSeeder
{
    private static readonly string[] GenreNames =
    [
        "Фантастика",
        "Фэнтези",
        "Детектив",
        "Роман",
        "Приключения",
        "Ужасы",
        "Триллер",
        "Исторический роман",
        "Поэзия",
        "Драма",
        "Комедия",
        "Научная литература",
        "Биография",
        "Детская литература"
    ];

    public static async Task SeedAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var existingNames = await db.Genres
            .Select(genre => genre.Name)
            .ToListAsync(cancellationToken);
        var knownNames = existingNames
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingGenres = GenreNames
            .Where(name => knownNames.Add(name))
            .Select(name => new Genre { Name = name })
            .ToArray();

        if (missingGenres.Length == 0)
            return;

        db.Genres.AddRange(missingGenres);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Добавлено начальных жанров: {GenreCount}", missingGenres.Length);
    }
}
