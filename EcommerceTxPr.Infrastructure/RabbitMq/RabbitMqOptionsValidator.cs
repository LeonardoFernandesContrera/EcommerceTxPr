using Microsoft.Extensions.Options;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

public sealed class RabbitMqOptionsValidator
    : IValidateOptions<RabbitMqOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        RabbitMqOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        AddRequiredFailure(failures, options.HostName, nameof(options.HostName));
        AddRequiredFailure(failures, options.UserName, nameof(options.UserName));
        AddRequiredFailure(failures, options.Password, nameof(options.Password));
        AddRequiredFailure(
            failures,
            options.VirtualHost,
            nameof(options.VirtualHost));
        AddRequiredFailure(
            failures,
            options.ExchangeName,
            nameof(options.ExchangeName));
        AddRequiredFailure(
            failures,
            options.PaymentEventsQueueName,
            nameof(options.PaymentEventsQueueName));

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("RabbitMq:Port must be between 1 and 65535.");
        }

        if (options.BatchSize is < 1 or > 1000)
        {
            failures.Add("RabbitMq:BatchSize must be between 1 and 1000.");
        }

        if (options.PollingIntervalSeconds is < 1 or > 3600)
        {
            failures.Add(
                "RabbitMq:PollingIntervalSeconds must be between 1 and 3600.");
        }

        if (options.PrefetchCount is < 1 or > 100)
        {
            failures.Add(
                "RabbitMq:PrefetchCount must be between 1 and 100.");
        }

        if (options.ConsumerReconnectDelaySeconds is < 1 or > 3600)
        {
            failures.Add(
                "RabbitMq:ConsumerReconnectDelaySeconds must be between 1 "
                + "and 3600.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddRequiredFailure(
        ICollection<string> failures,
        string value,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"RabbitMq:{propertyName} is required when enabled.");
        }
    }
}
