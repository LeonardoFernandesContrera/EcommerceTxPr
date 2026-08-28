using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.Application.Orders.Contracts;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    DateTime CreationDate,
    decimal Total,
    IReadOnlyCollection<OrderItemResponse> Items);
