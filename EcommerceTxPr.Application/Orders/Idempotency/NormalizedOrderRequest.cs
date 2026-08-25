namespace EcommerceTxPr.Application.Orders.Idempotency;

public sealed record NormalizedOrderItem(Guid ProductId, int Quantity);

public sealed record NormalizedOrderRequest(
    Guid CustomerId,
    IReadOnlyList<NormalizedOrderItem> Items,
    string RequestHash);
