using EcommerceTxPr.Infrastructure.Inbox;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IPaymentEventDeliveryHandler
{
    Task HandleAsync(
        PaymentIntegrationEventDelivery delivery,
        ulong deliveryTag,
        IPaymentDeliveryAcknowledger acknowledger,
        CancellationToken cancellationToken);
}
