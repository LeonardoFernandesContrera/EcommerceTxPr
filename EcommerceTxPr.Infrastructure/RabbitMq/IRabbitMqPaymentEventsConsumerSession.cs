namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IRabbitMqPaymentEventsConsumerSession : IAsyncDisposable
{
    Task Completion { get; }
}
