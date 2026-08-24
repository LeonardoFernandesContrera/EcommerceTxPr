using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Customers.Repositories
{
    public interface ICustomerRepository
    {
        Task<IReadOnlyCollection<Customer>> GetAllAsync(CancellationToken cancellationToken);
        Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task AddAsync(Customer customer, CancellationToken cancellationToken);
    }
}
