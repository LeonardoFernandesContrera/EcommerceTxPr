using System.Net;
using System.Text.Json;
using EcommerceTxPr.Infrastructure.Health;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Live_is_self_only_and_never_queries_dependencies()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: false);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: false);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health/live");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        var checks = body.RootElement.GetProperty("checks");
        Assert.Single(checks.EnumerateObject());
        Assert.Equal("Healthy", checks.GetProperty("self")
            .GetProperty("status").GetString());
        Assert.Equal(0, database.CallCount);
        Assert.Equal(0, rabbitMq.CallCount);
    }

    [Fact]
    public async Task Ready_queries_sql_only_when_sql_is_available()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: true);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: false);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health/ready");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, database.CallCount);
        Assert.Equal(0, rabbitMq.CallCount);
    }

    [Fact]
    public async Task Ready_returns_service_unavailable_when_sql_is_unavailable()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: false);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: true);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health/ready");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, database.CallCount);
        Assert.Equal(0, rabbitMq.CallCount);
    }

    [Fact]
    public async Task Full_health_is_healthy_when_sql_and_enabled_rabbitmq_are_available()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: true);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: true);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, database.CallCount);
        Assert.Equal(1, rabbitMq.CallCount);
    }

    [Fact]
    public async Task Full_health_is_degraded_but_successful_when_rabbitmq_is_unavailable()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: true);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: false);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", body.RootElement.GetProperty("status").GetString());
        var rabbitCheck = body.RootElement
            .GetProperty("checks")
            .GetProperty("rabbitmq");
        Assert.Equal("Degraded", rabbitCheck.GetProperty("status").GetString());
        Assert.DoesNotContain(
            "test-only-secret",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_health_is_unhealthy_when_sql_is_unavailable()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: false);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: true);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: true);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Full_health_reports_disabled_rabbitmq_as_healthy_without_querying_it()
    {
        var database = new RecordingDatabaseHealthProbe(isAvailable: true);
        var rabbitMq = new RecordingRabbitMqHealthProbe(isAvailable: false);
        using var factory = CreateFactory(database, rabbitMq, rabbitMqEnabled: false);
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health");
        using var body = await ReadBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "RabbitMQ disabled.",
            body.RootElement
                .GetProperty("checks")
                .GetProperty("rabbitmq")
                .GetProperty("description")
                .GetString());
        Assert.Equal(0, rabbitMq.CallCount);
    }

    private static CustomerApiFactory CreateFactory(
        IPrimaryDatabaseHealthProbe database,
        IRabbitMqHealthProbe rabbitMq,
        bool rabbitMqEnabled)
    {
        return new CustomerApiFactory(
            services =>
            {
                services.RemoveAll<IPrimaryDatabaseHealthProbe>();
                services.AddSingleton(database);
                services.RemoveAll<IRabbitMqHealthProbe>();
                services.AddSingleton(rabbitMq);
            },
            CreateRabbitMqConfiguration(rabbitMqEnabled));
    }

    private static Dictionary<string, string?> CreateRabbitMqConfiguration(
        bool enabled)
    {
        return new Dictionary<string, string?>
        {
            ["RabbitMq:Enabled"] = enabled.ToString(),
            ["RabbitMq:HostName"] = "127.0.0.1",
            ["RabbitMq:Port"] = "1",
            ["RabbitMq:UserName"] = "health-test",
            ["RabbitMq:Password"] = "test-only-secret",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:ExchangeName"] = "ecommerce.events",
            ["RabbitMq:PaymentEventsQueueName"] = "ecommerce.payment-events",
            ["RabbitMq:BatchSize"] = "20",
            ["RabbitMq:PollingIntervalSeconds"] = "60",
            ["RabbitMq:PrefetchCount"] = "1",
            ["RabbitMq:ConsumerReconnectDelaySeconds"] = "60"
        };
    }

    private static async Task<JsonDocument> ReadBodyAsync(
        HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private sealed class RecordingDatabaseHealthProbe(
        bool isAvailable) : IPrimaryDatabaseHealthProbe
    {
        public int CallCount { get; private set; }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(isAvailable);
        }
    }

    private sealed class RecordingRabbitMqHealthProbe(
        bool isAvailable) : IRabbitMqHealthProbe
    {
        public int CallCount { get; private set; }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(isAvailable);
        }
    }
}
