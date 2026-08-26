using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed record RabbitMqPublication(
    string Exchange,
    string RoutingKey,
    BasicProperties Properties,
    ReadOnlyMemory<byte> Body);
