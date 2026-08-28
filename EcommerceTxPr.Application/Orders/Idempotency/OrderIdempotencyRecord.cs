namespace EcommerceTxPr.Application.Orders.Idempotency;

public sealed class OrderIdempotencyRecord
{
    private OrderIdempotencyRecord()
    {
        KeyHash = string.Empty;
        RequestHash = string.Empty;
    }

    public OrderIdempotencyRecord(
        string keyHash,
        string requestHash,
        Guid orderId)
    {
        if (string.IsNullOrWhiteSpace(keyHash))
        {
            throw new ArgumentException(
                "Key hash must be supplied.",
                nameof(keyHash));
        }

        if (string.IsNullOrWhiteSpace(requestHash))
        {
            throw new ArgumentException(
                "Request hash must be supplied.",
                nameof(requestHash));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must be supplied.",
                nameof(orderId));
        }

        Id = Guid.NewGuid();
        KeyHash = keyHash;
        RequestHash = requestHash;
        OrderId = orderId;
        CreationDate = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public string KeyHash { get; private set; }

    public string RequestHash { get; private set; }

    public Guid OrderId { get; private set; }

    public DateTime CreationDate { get; private set; }
}
