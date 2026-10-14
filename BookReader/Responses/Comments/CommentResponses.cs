namespace BookReader.Responses.Comments;

public class CommentResponse
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public class CommentListResponse
{
    public IReadOnlyList<CommentResponse> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
