using System.Net;
using EcommerceTxPr.Infrastructure;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class RabbitMqStartupIsolationTests
{
    [Fact]
    public void Disabled_configuration_registers_no_rabbitmq_worker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddRabbitMqOutboxDispatcher(configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutboxMessagePublisher));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutboxDispatcher));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType
                == typeof(OutboxDispatcherBackgroundService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType
                == typeof(IPaymentIntegrationEventProcessor));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType
                == typeof(IRabbitMqPaymentEventsConsumerSessionFactory));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType
                == typeof(PaymentEventsConsumerBackgroundService));
    }

    [Fact]
    public async Task Invalid_enabled_configuration_fails_host_startup()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    CreateEnabledConfiguration(batchSize: 0)))
            .ConfigureServices((context, services) =>
                services.AddRabbitMqOutboxDispatcher(context.Configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.Contains("BatchSize", exception.Message);
    }

    [Fact]
    public async Task Enabled_unavailable_broker_does_not_block_api_or_business_request()
    {
        using var factory = new CustomerApiFactory(
            configurationValues: CreateEnabledConfiguration(port: 1));
        using var client = factory.CreateClientWithDatabase();

        using var healthResponse = await client.GetAsync("/health");
        var customer = await ApiTestData.CreateCustomerAsync(client);

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.NotEqual(Guid.Empty, customer.Id);
    }

    private static Dictionary<string, string?> CreateEnabledConfiguration(
        int port = 1,
        int batchSize = 20)
    {
        return new Dictionary<string, string?>
        {
            ["RabbitMq:Enabled"] = "true",
            ["RabbitMq:HostName"] = "127.0.0.1",
            ["RabbitMq:Port"] = port.ToString(),
            ["RabbitMq:UserName"] = "publisher",
            ["RabbitMq:Password"] = "test-only-secret",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:ExchangeName"] = "ecommerce.events",
            ["RabbitMq:PaymentEventsQueueName"] = "ecommerce.payment-events",
            ["RabbitMq:BatchSize"] = batchSize.ToString(),
            ["RabbitMq:PollingIntervalSeconds"] = "60",
            ["RabbitMq:PrefetchCount"] = "1",
            ["RabbitMq:ConsumerReconnectDelaySeconds"] = "60"
        };
    }
}
