namespace EcommerceTxPr.Application.Orders.Contracts;

public sealed record OrderCreationResponse(
    OrderResponse Order,
    OrderCreationStatus Status);
