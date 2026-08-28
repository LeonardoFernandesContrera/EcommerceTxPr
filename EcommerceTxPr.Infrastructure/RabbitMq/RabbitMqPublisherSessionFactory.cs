using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqPublisherSessionFactory
    : IRabbitMqPublisherSessionFactory
{
    private const string ConnectionName = "EcommerceTxPr.OutboxPublisher";

    private readonly RabbitMqOptions _options;

    public RabbitMqPublisherSessionFactory(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    internal static IReadOnlyList<string> RoutingKeys =>
        RabbitMqTopology.RoutingKeys;

    public async Task<IRabbitMqPublisherSession> CreateAsync(
        CancellationToken cancellationToken)
    {
        IConnection connection;

        try
        {
            connection = await CreateConnectionFactory(_options)
                .CreateConnectionAsync(ConnectionName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OutboxPublicationException(
                OutboxPublicationFailureCategory.Connection,
                exception);
        }

        IChannel channel;

        try
        {
            channel = await connection
                .CreateChannelAsync(CreateChannelOptions(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SafeDisposeAsync(connection).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await SafeDisposeAsync(connection).ConfigureAwait(false);
            throw new OutboxPublicationException(
                OutboxPublicationFailureCategory.Channel,
                exception);
        }

        try
        {
            await RabbitMqTopology
                .DeclareAsync(channel, _options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SafeDisposeAsync(channel).ConfigureAwait(false);
            await SafeDisposeAsync(connection).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await SafeDisposeAsync(channel).ConfigureAwait(false);
            await SafeDisposeAsync(connection).ConfigureAwait(false);
            throw new OutboxPublicationException(
                OutboxPublicationFailureCategory.Topology,
                exception);
        }

        return new RabbitMqPublisherSession(connection, channel);
    }

    internal static ConnectionFactory CreateConnectionFactory(
        RabbitMqOptions options)
    {
        return new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = ConnectionName
        };
    }

    internal static CreateChannelOptions CreateChannelOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
    }

    private static async ValueTask SafeDisposeAsync(IAsyncDisposable value)
    {
        try
        {
            await value.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Preserve the connection/channel/topology failure that caused cleanup.
        }
    }
}
