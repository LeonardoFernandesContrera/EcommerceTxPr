namespace EcommerceTxPr.Infrastructure.RabbitMq;

public sealed class OutboxPublicationException : Exception
{
    public OutboxPublicationException(
        OutboxPublicationFailureCategory category,
        Exception innerException)
        : base($"Outbox publication failed ({category}).", innerException)
    {
        Category = category;
    }

    public OutboxPublicationFailureCategory Category { get; }
}
