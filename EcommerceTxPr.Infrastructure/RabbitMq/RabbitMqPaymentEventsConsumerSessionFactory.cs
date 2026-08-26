using EcommerceTxPr.Infrastructure.Inbox;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqPaymentEventsConsumerSessionFactory
    : IRabbitMqPaymentEventsConsumerSessionFactory
{
    internal const string ConnectionName =
        "EcommerceTxPr.PaymentEventsConsumer";

    private readonly RabbitMqOptions _options;
    private readonly IPaymentEventDeliveryHandler _deliveryHandler;

    public RabbitMqPaymentEventsConsumerSessionFactory(
        IOptions<RabbitMqOptions> options,
        IPaymentEventDeliveryHandler deliveryHandler)
    {
        _options = options.Value;
        _deliveryHandler = deliveryHandler;
    }

    public async Task<IRabbitMqPaymentEventsConsumerSession> CreateAsync(
        CancellationToken cancellationToken)
    {
        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await CreateConnectionFactory(_options)
                .CreateConnectionAsync(ConnectionName, cancellationToken)
                .ConfigureAwait(false);
            channel = await connection.CreateChannelAsync(
                    CreateChannelOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
            await RabbitMqTopology
                .DeclareAsync(channel, _options, cancellationToken)
                .ConfigureAwait(false);

            var settings = CreateSubscriptionSettings(_options);
            await channel.BasicQosAsync(
                    prefetchSize: 0,
                    settings.PrefetchCount,
                    global: false,
                    cancellationToken)
                .ConfigureAwait(false);

            var completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            connection.ConnectionShutdownAsync += (_, _) =>
            {
                completion.TrySetResult(null);
                return Task.CompletedTask;
            };
            channel.ChannelShutdownAsync += (_, _) =>
            {
                completion.TrySetResult(null);
                return Task.CompletedTask;
            };
            channel.CallbackExceptionAsync += (_, args) =>
            {
                completion.TrySetException(args.Exception);
                return Task.CompletedTask;
            };

            var acknowledger = new RabbitMqPaymentDeliveryAcknowledger(channel);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, args) =>
            {
                var delivery = CopyDelivery(args);
                await _deliveryHandler
                    .HandleAsync(
                        delivery,
                        args.DeliveryTag,
                        acknowledger,
                        args.CancellationToken)
                    .ConfigureAwait(false);
            };

            var consumerTag = await channel.BasicConsumeAsync(
                    settings.QueueName,
                    settings.AutoAck,
                    consumer,
                    cancellationToken)
                .ConfigureAwait(false);

            return new RabbitMqPaymentEventsConsumerSession(
                connection,
                channel,
                consumerTag,
                completion.Task);
        }
        catch
        {
            if (channel is not null)
            {
                await SafeDisposeAsync(channel).ConfigureAwait(false);
            }

            if (connection is not null)
            {
                await SafeDisposeAsync(connection).ConfigureAwait(false);
            }

            throw;
        }
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
            ConsumerDispatchConcurrency = 1,
            ClientProvidedName = ConnectionName
        };
    }

    internal static RabbitMqConsumerSubscriptionSettings
        CreateSubscriptionSettings(RabbitMqOptions options)
    {
        return new RabbitMqConsumerSubscriptionSettings(
            options.PaymentEventsQueueName,
            AutoAck: false,
            options.PrefetchCount);
    }

    internal static PaymentIntegrationEventDelivery CopyDelivery(
        BasicDeliverEventArgs args)
    {
        return new PaymentIntegrationEventDelivery(
            args.BasicProperties.MessageId,
            args.BasicProperties.Type,
            args.RoutingKey,
            args.Body.ToArray());
    }

    private static CreateChannelOptions CreateChannelOptions()
    {
        return new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false);
    }

    private static async ValueTask SafeDisposeAsync(IAsyncDisposable value)
    {
        try
        {
            await value.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Preserve the session-creation failure that caused cleanup.
        }
    }
}
