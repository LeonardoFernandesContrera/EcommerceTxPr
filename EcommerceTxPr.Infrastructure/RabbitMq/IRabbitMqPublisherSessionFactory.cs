namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IRabbitMqPublisherSessionFactory
{
    Task<IRabbitMqPublisherSession> CreateAsync(
        CancellationToken cancellationToken);
}
