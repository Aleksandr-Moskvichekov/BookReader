using System.ComponentModel.DataAnnotations;

namespace BookReader.Requests.Users;

public class UpdateProfileRequest
{
    [Required]
    [StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
