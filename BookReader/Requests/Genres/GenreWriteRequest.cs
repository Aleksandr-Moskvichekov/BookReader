using System.ComponentModel.DataAnnotations;

namespace BookReader.Requests.Genres;

public class GenreWriteRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
