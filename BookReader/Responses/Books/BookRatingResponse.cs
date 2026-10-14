namespace BookReader.Responses.Books;

public class BookRatingResponse
{
    public double? AverageRating { get; init; }
    public int RatingCount { get; init; }
    public int? MyRating { get; init; }
}
