using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.Infrastructure.Health;

internal sealed class PrimaryDatabaseHealthProbe(
    IServiceScopeFactory scopeFactory) : IPrimaryDatabaseHealthProbe
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public async Task<bool> CanConnectAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();

        return await context.Database
            .CanConnectAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
