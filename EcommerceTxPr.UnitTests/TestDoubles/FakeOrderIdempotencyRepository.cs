using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Application.Orders.Repositories;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeOrderIdempotencyRepository
    : IOrderIdempotencyRepository
{
    private readonly Queue<OrderIdempotencyRecord?> _getResults = new();

    public List<string> GetByKeyHashRequests { get; } = new();

    public List<OrderIdempotencyRecord> AddedRecords { get; } = new();

    public void EnqueueGetResult(OrderIdempotencyRecord? record)
    {
        _getResults.Enqueue(record);
    }

    public Task<OrderIdempotencyRecord?> GetByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken)
    {
        GetByKeyHashRequests.Add(keyHash);
        var result = _getResults.Count == 0 ? null : _getResults.Dequeue();
        return Task.FromResult(result);
    }

    public Task AddAsync(
        OrderIdempotencyRecord record,
        CancellationToken cancellationToken)
    {
        AddedRecords.Add(record);
        return Task.CompletedTask;
    }
}
