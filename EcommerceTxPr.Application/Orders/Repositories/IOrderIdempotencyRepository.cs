using EcommerceTxPr.Application.Orders.Idempotency;

namespace EcommerceTxPr.Application.Orders.Repositories;

public interface IOrderIdempotencyRepository
{
    Task<OrderIdempotencyRecord?> GetByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken);

    Task AddAsync(
        OrderIdempotencyRecord record,
        CancellationToken cancellationToken);
}
