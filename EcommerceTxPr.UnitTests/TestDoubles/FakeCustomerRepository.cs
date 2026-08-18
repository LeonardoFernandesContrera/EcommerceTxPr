using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeCustomerRepository : ICustomerRepository
{
    public IReadOnlyCollection<Customer> GetAllResult { get; set; } =
        Array.Empty<Customer>();

    public Customer? GetByIdResult { get; set; }

    public List<Customer> AddedCustomers { get; } = new();

    public List<Customer> UpdatedCustomers { get; } = new();

    public Task<IReadOnlyCollection<Customer>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(GetAllResult);
    }

    public Task<Customer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(GetByIdResult);
    }

    public Task AddAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        AddedCustomers.Add(customer);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        Customer customer,
        CancellationToken cancellationToken)
    {
        UpdatedCustomers.Add(customer);
        return Task.CompletedTask;
    }
}
