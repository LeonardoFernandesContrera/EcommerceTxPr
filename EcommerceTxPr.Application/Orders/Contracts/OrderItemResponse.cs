namespace EcommerceTxPr.Application.Orders.Contracts;

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
