using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Contracts;

namespace EcommerceTxPr.Application.Orders.Services;

public interface IOrderService
{
    Task<Result<OrderResponse, Error>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<OrderCreationResponse, Error>> CreateAsync(
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
