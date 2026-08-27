using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly Queue<Payment?> _processingResults = new();

    public Payment? GetByIdResult { get; set; }

    public Payment? GetByOrderIdResult { get; set; }

    public Payment? GetByOrderIdForProcessingResult { get; set; }

    public List<Guid> GetByIdRequests { get; } = new();

    public List<Guid> GetByOrderIdRequests { get; } = new();

    public List<Guid> GetByOrderIdForProcessingRequests { get; } = new();

    public List<Payment> AddedPayments { get; } = new();

    public void EnqueueProcessingResult(Payment? payment)
    {
        _processingResults.Enqueue(payment);
    }

    public Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        GetByIdRequests.Add(id);
        return Task.FromResult(GetByIdResult);
    }

    public Task<Payment?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        GetByOrderIdRequests.Add(orderId);
        return Task.FromResult(GetByOrderIdResult);
    }

    public Task<Payment?> GetByOrderIdForProcessingAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        GetByOrderIdForProcessingRequests.Add(orderId);
        var result = _processingResults.Count > 0
            ? _processingResults.Dequeue()
            : GetByOrderIdForProcessingResult;
        return Task.FromResult(result);
    }

    public Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        AddedPayments.Add(payment);
        return Task.CompletedTask;
    }
}
