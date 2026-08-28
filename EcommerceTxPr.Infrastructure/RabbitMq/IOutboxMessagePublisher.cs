using EcommerceTxPr.Infrastructure.Outbox;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken);
}
