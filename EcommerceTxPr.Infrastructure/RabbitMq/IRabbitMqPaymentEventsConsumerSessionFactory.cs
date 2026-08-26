namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IRabbitMqPaymentEventsConsumerSessionFactory
{
    Task<IRabbitMqPaymentEventsConsumerSession> CreateAsync(
        CancellationToken cancellationToken);
}
