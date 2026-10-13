using System.ComponentModel.DataAnnotations;

namespace BookReader.Recuests.Admin
{
    public class ChangeUserRoleRequest
    {
        [Required]
        public string Role { get; set; } = string.Empty;
    }
}
