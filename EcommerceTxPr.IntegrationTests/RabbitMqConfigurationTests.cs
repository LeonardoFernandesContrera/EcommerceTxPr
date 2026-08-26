using EcommerceTxPr.Infrastructure.RabbitMq;

namespace EcommerceTxPr.IntegrationTests;

public sealed class RabbitMqConfigurationTests
{
    private readonly RabbitMqOptionsValidator _validator = new();

    [Fact]
    public void Disabled_default_configuration_is_valid_without_credentials()
    {
        var result = _validator.Validate(null, new RabbitMqOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_complete_configuration_is_valid()
    {
        var result = _validator.Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("HostName")]
    [InlineData("UserName")]
    [InlineData("Password")]
    [InlineData("VirtualHost")]
    [InlineData("ExchangeName")]
    [InlineData("PaymentEventsQueueName")]
    public void Enabled_configuration_rejects_blank_required_value(
        string propertyName)
    {
        var options = CreateValidOptions();
        SetStringProperty(options, propertyName, "   ");

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(propertyName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Enabled_configuration_rejects_invalid_port(int port)
    {
        var options = CreateValidOptions();
        options.Port = port;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Port"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Enabled_configuration_rejects_invalid_batch_size(int batchSize)
    {
        var options = CreateValidOptions();
        options.BatchSize = batchSize;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("BatchSize"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Enabled_configuration_rejects_invalid_polling_interval(
        int pollingIntervalSeconds)
    {
        var options = CreateValidOptions();
        options.PollingIntervalSeconds = pollingIntervalSeconds;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("PollingIntervalSeconds"));
    }

    private static RabbitMqOptions CreateValidOptions()
    {
        return new RabbitMqOptions
        {
            Enabled = true,
            HostName = "localhost",
            Port = 5672,
            UserName = "publisher",
            Password = "development-secret",
            VirtualHost = "/",
            ExchangeName = "ecommerce.events",
            PaymentEventsQueueName = "ecommerce.payment-events",
            BatchSize = 20,
            PollingIntervalSeconds = 5
        };
    }

    private static void SetStringProperty(
        RabbitMqOptions options,
        string propertyName,
        string value)
    {
        switch (propertyName)
        {
            case "HostName":
                options.HostName = value;
                break;
            case "UserName":
                options.UserName = value;
                break;
            case "Password":
                options.Password = value;
                break;
            case "VirtualHost":
                options.VirtualHost = value;
                break;
            case "ExchangeName":
                options.ExchangeName = value;
                break;
            case "PaymentEventsQueueName":
                options.PaymentEventsQueueName = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyName));
        }
    }
}
