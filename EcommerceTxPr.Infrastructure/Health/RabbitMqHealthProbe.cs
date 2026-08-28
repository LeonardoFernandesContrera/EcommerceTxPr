using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.Health;

internal sealed class RabbitMqHealthProbe : IRabbitMqHealthProbe
{
    internal const string ConnectionName = "EcommerceTxPr.HealthCheck";

    private static readonly TimeSpan ConnectionTimeout =
        TimeSpan.FromSeconds(2);

    private readonly RabbitMqOptions _options;

    public RabbitMqHealthProbe(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<bool> CanConnectAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        await using var connection = await CreateConnectionFactory(_options)
            .CreateConnectionAsync(ConnectionName, cancellationToken)
            .ConfigureAwait(false);

        return connection.IsOpen;
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
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            RequestedConnectionTimeout = ConnectionTimeout,
            HandshakeContinuationTimeout = ConnectionTimeout,
            ClientProvidedName = ConnectionName
        };
    }
}
