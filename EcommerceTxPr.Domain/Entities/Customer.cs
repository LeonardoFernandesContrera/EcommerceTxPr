using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Domain.Entities
{
    public class Customer : BaseEntity
    {
        [Required]
        [MaxLength(60)]
        public string Name { get; private set; }


        [Required]
        public DateTime BirthDate { get; private set; }

        public Customer(string name, DateTime birthDate)
        {
            Name = name;
            BirthDate = birthDate;
        }

        public void UpdateDetails(string name, DateTime birthDate)
        {
            Name = name;
            BirthDate = birthDate;
        }
    }
}
