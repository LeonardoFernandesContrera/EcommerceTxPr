using EcommerceTxPr.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EcommerceApi.V2.Health;

internal sealed class PrimaryDatabaseHealthCheck(
    IPrimaryDatabaseHealthProbe probe) : IHealthCheck
{
    private readonly IPrimaryDatabaseHealthProbe _probe = probe;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _probe
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return isAvailable
                ? HealthCheckResult.Healthy("SQL Server available.")
                : HealthCheckResult.Unhealthy("SQL Server unavailable.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server unavailable.");
        }
    }
}
