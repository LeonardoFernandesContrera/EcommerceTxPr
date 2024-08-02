using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Entities
{
    public class Login : BaseEntity
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [PasswordPropertyText]
        public string Password { get; set; }
    }
}