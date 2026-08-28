using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers.Contracts;

namespace EcommerceTxPr.Application.Customers.Services
{
    public interface ICustomerService
    {
        Task<Result<IReadOnlyCollection<CustomerResponse>, Error>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<Result<CustomerResponse, Error>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<Result<CustomerResponse, Error>> CreateAsync(
            CreateCustomerRequest request,
            CancellationToken cancellationToken);

        Task<Result<CustomerResponse, Error>> UpdateAsync(
            Guid id,
            UpdateCustomerRequest request,
            CancellationToken cancellationToken);

        Task<Result<Guid, Error>> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}
