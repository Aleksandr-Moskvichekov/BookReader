using System.ComponentModel.DataAnnotations;

namespace BookReader.Recuests.Admin
{
    public class BanUserRequest
    {
        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }
}
