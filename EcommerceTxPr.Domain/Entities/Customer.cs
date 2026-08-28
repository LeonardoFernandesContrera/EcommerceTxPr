namespace EcommerceTxPr.Domain.Entities
{
    public class Customer : BaseEntity
    {
        public string Name { get; private set; }

        public DateTime BirthDate { get; private set; }

        public bool IsActive { get; private set; }

        public Customer(string name, DateTime birthDate)
        {
            Name = name;
            BirthDate = birthDate;
            IsActive = true;
        }

        public void UpdateDetails(string name, DateTime birthDate)
        {
            Name = name;
            BirthDate = birthDate;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
