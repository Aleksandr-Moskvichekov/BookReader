namespace BookReader.Data.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PublishYear { get; set; }
        public string? CoverKey { get; set; }       // ключ обложки в MinIO
        public int ViewsCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int AuthorId { get; set; }
        public User Author { get; set; } = null!;

        public ICollection<Genre> Genres { get; set; } = new List<Genre>();
        public ICollection<BookFile> Files { get; set; } = new List<BookFile>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
