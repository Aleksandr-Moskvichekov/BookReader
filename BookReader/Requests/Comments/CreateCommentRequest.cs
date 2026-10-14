using System.ComponentModel.DataAnnotations;

namespace BookReader.Requests.Comments;

public class CreateCommentRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
}
