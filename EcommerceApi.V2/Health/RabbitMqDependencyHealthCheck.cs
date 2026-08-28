using EcommerceTxPr.Infrastructure.Health;
using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EcommerceApi.V2.Health;

internal sealed class RabbitMqDependencyHealthCheck : IHealthCheck
{
    private readonly IRabbitMqHealthProbe _probe;
    private readonly RabbitMqOptions _options;

    public RabbitMqDependencyHealthCheck(
        IRabbitMqHealthProbe probe,
        IOptions<RabbitMqOptions> options)
    {
        _probe = probe;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("RabbitMQ disabled.");
        }

        try
        {
            var isAvailable = await _probe
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return isAvailable
                ? HealthCheckResult.Healthy("RabbitMQ available.")
                : HealthCheckResult.Degraded("RabbitMQ unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Degraded("RabbitMQ unavailable.");
        }
    }
}
