using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Domain.Entities
{
    public class BaseEntity
    {
        [Key]
        public Guid Id { get; private set; }
        [Required]
        public DateTime CreationDate { get; private set; }

        [Required]
        public bool IsActive { get; private set; }

        protected BaseEntity() 
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
            IsActive = true;
        }

        public void TurnIsActiveToFalse()
        {
            IsActive = false;
        }
    }
}
