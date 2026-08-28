namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal interface IPaymentDeliveryAcknowledger
{
    Task AckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken);

    Task RejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken);

    Task NackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken);
}
