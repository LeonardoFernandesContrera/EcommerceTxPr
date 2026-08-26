namespace EcommerceTxPr.Infrastructure.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "ecommerce.events";

    public string PaymentEventsQueueName { get; set; } =
        "ecommerce.payment-events";

    public int BatchSize { get; set; } = 20;

    public int PollingIntervalSeconds { get; set; } = 5;

    public ushort PrefetchCount { get; set; } = 1;

    public int ConsumerReconnectDelaySeconds { get; set; } = 5;
}
