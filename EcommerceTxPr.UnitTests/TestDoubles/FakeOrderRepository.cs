using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly Queue<Order?> _getByIdForPaymentResults = new();

    public Order? GetByIdResult { get; set; }

    public Order? GetByIdForPaymentResult { get; set; }

    public List<Guid> GetByIdRequests { get; } = new();

    public List<Guid> GetByIdForPaymentRequests { get; } = new();

    public List<Order> AddedOrders { get; } = new();

    public void EnqueueGetByIdForPaymentResult(Order? order)
    {
        _getByIdForPaymentResults.Enqueue(order);
    }

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
        var result = _getByIdForPaymentResults.Count > 0
            ? _getByIdForPaymentResults.Dequeue()
            : GetByIdForPaymentResult;
        return Task.FromResult(result);
    }

    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        AddedOrders.Add(order);
        return Task.CompletedTask;
    }
}
