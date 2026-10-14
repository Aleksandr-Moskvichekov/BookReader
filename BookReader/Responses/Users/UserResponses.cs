using BookReader.Responses.Books;

namespace BookReader.Responses.Users;

public class PublicUserProfileResponse
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class UserBookResponse : BookSummaryResponse
{
    public DateTime CreatedAt { get; init; }
    public DateTime? AddedToFavoritesAt { get; init; }
    public DateTime? LastAccessedAt { get; init; }
}

public class UserBookListResponse
{
    public IReadOnlyList<UserBookResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
