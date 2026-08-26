namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IRabbitMqPublisherSession : IAsyncDisposable
{
    Task PublishAsync(
        RabbitMqPublication publication,
        CancellationToken cancellationToken);
}
