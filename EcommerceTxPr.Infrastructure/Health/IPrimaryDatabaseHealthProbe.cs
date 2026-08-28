namespace EcommerceTxPr.Infrastructure.Health;

public interface IPrimaryDatabaseHealthProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
