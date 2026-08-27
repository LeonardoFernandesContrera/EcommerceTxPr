namespace EcommerceTxPr.Infrastructure.Health;

public interface IRabbitMqHealthProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
