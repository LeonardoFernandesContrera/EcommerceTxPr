using EcommerceTxPr.Infrastructure.Inbox;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IPaymentEventDeliveryHandler
{
    Task<PaymentEventDeliveryHandlingResult> HandleAsync(
        PaymentIntegrationEventDelivery delivery,
        ulong deliveryTag,
        IPaymentDeliveryAcknowledger acknowledger,
        CancellationToken cancellationToken);
}
