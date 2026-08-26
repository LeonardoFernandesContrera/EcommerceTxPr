using System.Text;
using EcommerceTxPr.Infrastructure.Outbox;
using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal static class RabbitMqPublicationFactory
{
    public static RabbitMqPublication Create(
        OutboxMessage message,
        RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.Id.ToString("D"),
            Type = message.Type,
            Persistent = true
        };

        return new RabbitMqPublication(
            options.ExchangeName,
            message.Type,
            properties,
            Encoding.UTF8.GetBytes(message.Payload));
    }
}
