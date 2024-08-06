using System.ComponentModel.DataAnnotations;

namespace EcommerceApi.Entities
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
            CreationDate = DateTime.Now;
            IsActive = true;
        }

        public void TurnIsActiveToFalse()
        {
            IsActive = false;
        }

        public void ChangeId(Guid guid)
        {
            Id = guid;
        }
    }
}
