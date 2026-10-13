namespace BookReader.Data.Models
{
    public class BookFile
    {
        public int Id { get; set; }
        public BookFormat Format { get; set; }
        public string StorageKey { get; set; } = string.Empty;  // ключ объекта в S3-хранилище
        public long SizeBytes { get; set; }

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;
    }
}
