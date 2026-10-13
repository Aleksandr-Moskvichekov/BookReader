namespace BookReader.Responses.Admin
{
    public class AdminUserResponse
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public bool IsBootstrapAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
