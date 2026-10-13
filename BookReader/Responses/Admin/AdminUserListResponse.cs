namespace BookReader.Responses.Admin
{
    public class AdminUserListResponse
    {
        public IReadOnlyList<AdminUserResponse> Users { get; set; } = Array.Empty<AdminUserResponse>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
