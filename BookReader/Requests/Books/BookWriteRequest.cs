using System.ComponentModel.DataAnnotations;

namespace BookReader.Requests.Books;

public class BookWriteRequest
{
    [Required]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(1, 9999)]
    public int PublishYear { get; set; }

    public List<int> GenreIds { get; set; } = [];
}
