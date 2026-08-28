namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed record RabbitMqConsumerSubscriptionSettings(
    string QueueName,
    bool AutoAck,
    ushort PrefetchCount);
