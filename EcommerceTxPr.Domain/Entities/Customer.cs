using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Entities
{
    public class Customer : BaseEntity
    {
        [Required]
        [MaxLength(60)]
        public string Name { get;  set; }


        [Required]
        public DateTime BirthDate { get; set; }

        public Customer(string name, DateTime birthDate)
        {
            Name = name;
            BirthDate = birthDate;
        }
    }
}
