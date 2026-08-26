using System.Text;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;

namespace EcommerceTxPr.IntegrationTests;

public sealed class RabbitMqPublicationTests
{
    [Fact]
    public void Publication_uses_exact_persisted_identity_type_and_payload()
    {
        var options = CreateOptions();
        var payload =
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\",\"amount\":25.50}";
        var message = new OutboxMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            payload,
            new DateTime(2026, 8, 25, 12, 30, 0, DateTimeKind.Utc));

        var publication = RabbitMqPublicationFactory.Create(message, options);

        Assert.Equal("ecommerce.events", publication.Exchange);
        Assert.Equal("payment.succeeded.v1", publication.RoutingKey);
        Assert.Equal("application/json", publication.Properties.ContentType);
        Assert.Equal(message.Id.ToString("D"), publication.Properties.MessageId);
        Assert.Equal("payment.succeeded.v1", publication.Properties.Type);
        Assert.True(publication.Properties.Persistent);
        Assert.Equal(payload, Encoding.UTF8.GetString(publication.Body.Span));
    }

    [Fact]
    public void Topology_binds_exactly_the_supported_payment_event_types()
    {
        Assert.Equal(
            new[]
            {
                "payment.succeeded.v1",
                "payment.failed.v1"
            },
            RabbitMqPublisherSessionFactory.RoutingKeys);
    }

    [Fact]
    public void Connection_factory_uses_validated_endpoint_and_recovery_settings()
    {
        var options = CreateOptions();

        var factory = RabbitMqPublisherSessionFactory
            .CreateConnectionFactory(options);

        Assert.Equal("broker.internal", factory.HostName);
        Assert.Equal(5678, factory.Port);
        Assert.Equal("publisher", factory.UserName);
        Assert.Equal("secret", factory.Password);
        Assert.Equal("/ecommerce", factory.VirtualHost);
        Assert.True(factory.AutomaticRecoveryEnabled);
        Assert.True(factory.TopologyRecoveryEnabled);
    }

    [Fact]
    public void Channel_options_enable_confirms_and_return_correlation()
    {
        var options = RabbitMqPublisherSessionFactory.CreateChannelOptions();

        Assert.True(options.PublisherConfirmationsEnabled);
        Assert.True(options.PublisherConfirmationTrackingEnabled);
    }

    private static RabbitMqOptions CreateOptions()
    {
        return new RabbitMqOptions
        {
            Enabled = true,
            HostName = "broker.internal",
            Port = 5678,
            UserName = "publisher",
            Password = "secret",
            VirtualHost = "/ecommerce",
            ExchangeName = "ecommerce.events",
            PaymentEventsQueueName = "ecommerce.payment-events"
        };
    }
}
