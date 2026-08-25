using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Orders.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Order?> GetByIdForPaymentAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddAsync(Order order, CancellationToken cancellationToken);
}
