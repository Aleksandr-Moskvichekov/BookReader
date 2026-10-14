namespace BookReader.Data.Models;

public class UserBookHistory
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;

    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}
