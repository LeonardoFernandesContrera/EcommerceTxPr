using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeOrderRepository : IOrderRepository
{
    public Order? GetByIdResult { get; set; }

    public Order? GetByIdForPaymentResult { get; set; }

    public List<Guid> GetByIdRequests { get; } = new();

    public List<Guid> GetByIdForPaymentRequests { get; } = new();

    public List<Order> AddedOrders { get; } = new();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        GetByIdRequests.Add(id);
        return Task.FromResult(GetByIdResult);
    }

    public Task<Order?> GetByIdForPaymentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        GetByIdForPaymentRequests.Add(id);
        return Task.FromResult(GetByIdForPaymentResult);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        AddedOrders.Add(order);
        return Task.CompletedTask;
    }
}
