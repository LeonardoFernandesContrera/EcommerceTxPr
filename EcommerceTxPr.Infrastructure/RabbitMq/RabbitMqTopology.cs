using EcommerceTxPr.Infrastructure.Outbox;
using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal static class RabbitMqTopology
{
    private static readonly IReadOnlyList<string> SupportedRoutingKeys =
        Array.AsReadOnly(new[]
        {
            OutboxMessageTypes.PaymentSucceededV1,
            OutboxMessageTypes.PaymentFailedV1
        });

    public static IReadOnlyList<string> RoutingKeys => SupportedRoutingKeys;

    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
                options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await channel.QueueDeclareAsync(
                options.PaymentEventsQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var routingKey in SupportedRoutingKeys)
        {
            await channel.QueueBindAsync(
                    options.PaymentEventsQueueName,
                    options.ExchangeName,
                    routingKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
