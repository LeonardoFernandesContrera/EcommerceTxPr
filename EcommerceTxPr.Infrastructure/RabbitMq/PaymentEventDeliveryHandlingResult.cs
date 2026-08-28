namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal enum PaymentEventDeliveryHandlingResult
{
    Continue = 0,
    EndSession = 1
}
