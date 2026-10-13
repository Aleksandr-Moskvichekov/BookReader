using System.ComponentModel.DataAnnotations;

namespace BookReader.Recuests.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}