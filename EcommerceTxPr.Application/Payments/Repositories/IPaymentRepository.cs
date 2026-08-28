using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Payments.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Payment?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Payment?> GetByOrderIdForProcessingAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken);
}
