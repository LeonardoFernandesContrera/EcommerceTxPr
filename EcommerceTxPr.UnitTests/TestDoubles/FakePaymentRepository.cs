using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakePaymentRepository : IPaymentRepository
{
    public Payment? GetByIdResult { get; set; }

    public Payment? GetByOrderIdResult { get; set; }

    public List<Guid> GetByIdRequests { get; } = new();

    public List<Guid> GetByOrderIdRequests { get; } = new();

    public List<Payment> AddedPayments { get; } = new();

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

    public Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        AddedPayments.Add(payment);
        return Task.CompletedTask;
    }
}
