using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeOrderRepository : IOrderRepository
{
    public Order? GetByIdResult { get; set; }

    public List<Order> AddedOrders { get; } = new();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(GetByIdResult);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        AddedOrders.Add(order);
        return Task.CompletedTask;
    }
}
