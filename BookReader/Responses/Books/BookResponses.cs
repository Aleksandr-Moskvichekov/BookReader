using BookReader.Data.Models;

namespace BookReader.Responses.Books;

public class BookSummaryResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public int PublishYear { get; init; }
    public string Author { get; init; } = string.Empty;
    public IReadOnlyList<string> Genres { get; init; } = [];
}

public class BookDetailsResponse : BookSummaryResponse
{
    public string Description { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
    public int ViewsCount { get; init; }
    public double? AverageRating { get; init; }
    public int RatingCount { get; init; }
    public IReadOnlyList<BookFileResponse> Files { get; init; } = [];
}

public class BookFileResponse
{
    public int Id { get; init; }
    public BookFormat Format { get; init; }
    public long SizeBytes { get; init; }
}

public class BookCatalogResponse
{
    public IReadOnlyList<BookSummaryResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public class CollectionBookResponse : BookSummaryResponse
{
    public double? AverageRating { get; init; }
    public int ViewsCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class BookCollectionResponse
{
    public IReadOnlyList<CollectionBookResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
