namespace EcommerceTxPr.Infrastructure.Inbox;

internal interface IPaymentIntegrationEventProcessor
{
    Task<PaymentIntegrationEventProcessingResult> ProcessAsync(
        PaymentIntegrationEventDelivery delivery,
        CancellationToken cancellationToken);
}
