namespace BookReader.Data.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public int Score { get; set; }   // 1..5

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
