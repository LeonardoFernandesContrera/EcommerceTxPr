using System.Reflection;
using System.Text;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EcommerceTxPr.IntegrationTests;

public sealed class RabbitMqConsumerSessionTests
{
    [Fact]
    public void Consumer_connection_factory_has_distinct_name_and_sequential_dispatch()
    {
        var options = CreateOptions();

        var factory = RabbitMqPaymentEventsConsumerSessionFactory
            .CreateConnectionFactory(options);

        Assert.Equal("broker.internal", factory.HostName);
        Assert.Equal(5678, factory.Port);
        Assert.Equal("consumer", factory.UserName);
        Assert.Equal("secret", factory.Password);
        Assert.Equal("/ecommerce", factory.VirtualHost);
        Assert.Equal(
            "EcommerceTxPr.PaymentEventsConsumer",
            factory.ClientProvidedName);
        Assert.Equal((ushort)1, factory.ConsumerDispatchConcurrency);
        Assert.True(factory.AutomaticRecoveryEnabled);
        Assert.True(factory.TopologyRecoveryEnabled);
    }

    [Fact]
    public void Subscription_settings_use_manual_ack_and_prefetch_one()
    {
        var settings = RabbitMqPaymentEventsConsumerSessionFactory
            .CreateSubscriptionSettings(CreateOptions());

        Assert.Equal("ecommerce.payment-events", settings.QueueName);
        Assert.False(settings.AutoAck);
        Assert.Equal((ushort)1, settings.PrefetchCount);
    }

    [Fact]
    public void Delivery_copy_preserves_metadata_and_owns_body_memory()
    {
        var originalBody = Encoding.UTF8.GetBytes("{\"value\":1}");
        var properties = new BasicProperties
        {
            MessageId = "11111111-1111-1111-1111-111111111111",
            Type = OutboxMessageTypes.PaymentSucceededV1
        };
        var args = new BasicDeliverEventArgs(
            "consumer-tag",
            deliveryTag: 42,
            redelivered: false,
            exchange: "ecommerce.events",
            routingKey: OutboxMessageTypes.PaymentSucceededV1,
            properties,
            originalBody,
            CancellationToken.None);

        var delivery = RabbitMqPaymentEventsConsumerSessionFactory
            .CopyDelivery(args);
        originalBody[0] = (byte)'X';

        Assert.Equal(properties.MessageId, delivery.MessageId);
        Assert.Equal(properties.Type, delivery.Type);
        Assert.Equal(args.RoutingKey, delivery.RoutingKey);
        Assert.Equal("{\"value\":1}", Encoding.UTF8.GetString(delivery.Body));
    }

    [Fact]
    public void Publisher_and_consumer_share_exact_payment_topology_keys()
    {
        Assert.Equal(
            new[]
            {
                OutboxMessageTypes.PaymentSucceededV1,
                OutboxMessageTypes.PaymentFailedV1
            },
            RabbitMqTopology.RoutingKeys);
        Assert.Same(
            RabbitMqTopology.RoutingKeys,
            RabbitMqPublisherSessionFactory.RoutingKeys);
    }

    [Fact]
    public async Task Broker_consumer_cancellation_completes_session_for_reconnect()
    {
        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = DispatchProxy.Create<IChannel, ThrowingChannelProxy>();
        var consumer = new AsyncEventingBasicConsumer(channel);
        RabbitMqPaymentEventsConsumerSessionFactory
            .ObserveConsumerCancellation(consumer, completion);

        await consumer.HandleBasicCancelAsync(
            "broker-cancelled-consumer",
            CancellationToken.None);

        Assert.True(completion.Task.IsCompletedSuccessfully);
    }

    private static RabbitMqOptions CreateOptions()
    {
        return new RabbitMqOptions
        {
            Enabled = true,
            HostName = "broker.internal",
            Port = 5678,
            UserName = "consumer",
            Password = "secret",
            VirtualHost = "/ecommerce",
            ExchangeName = "ecommerce.events",
            PaymentEventsQueueName = "ecommerce.payment-events",
            PrefetchCount = 1
        };
    }

    public class ThrowingChannelProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new InvalidOperationException(
                $"Unexpected channel call: {targetMethod?.Name}.");
        }
    }
}
