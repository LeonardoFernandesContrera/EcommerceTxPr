using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqPaymentEventsConsumerSession
    : IRabbitMqPaymentEventsConsumerSession
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _consumerTag;

    public RabbitMqPaymentEventsConsumerSession(
        IConnection connection,
        IChannel channel,
        string consumerTag,
        Task completion)
    {
        _connection = connection;
        _channel = channel;
        _consumerTag = consumerTag;
        Completion = completion;
    }

    public Task Completion { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_channel.IsOpen)
            {
                try
                {
                    await _channel.BasicCancelAsync(_consumerTag)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // The session may already be unavailable or cancelled.
                }
            }
        }
        finally
        {
            try
            {
                await _channel.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
