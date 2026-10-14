using System.ComponentModel.DataAnnotations;

namespace BookReader.Requests.Books;

public class RateBookRequest
{
    [Range(1, 5)]
    public int Score { get; set; }
}
