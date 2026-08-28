using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqPublisherSession : IRabbitMqPublisherSession
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RabbitMqPublisherSession(
        IConnection connection,
        IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public async Task PublishAsync(
        RabbitMqPublication publication,
        CancellationToken cancellationToken)
    {
        await _channel.BasicPublishAsync(
            publication.Exchange,
            publication.RoutingKey,
            mandatory: true,
            publication.Properties,
            publication.Body,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _channel.DisposeAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }
}
