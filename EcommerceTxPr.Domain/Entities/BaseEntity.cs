namespace EcommerceTxPr.Domain.Entities
{
    public abstract class BaseEntity
    {
        public Guid Id { get; private set; }

        public DateTime CreationDate { get; private set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            CreationDate = DateTime.UtcNow;
        }
    }
}
