using RabbitMQ.Client;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqPaymentDeliveryAcknowledger
    : IPaymentDeliveryAcknowledger
{
    private readonly IChannel _channel;

    public RabbitMqPaymentDeliveryAcknowledger(IChannel channel)
    {
        _channel = channel;
    }

    public async Task AckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        await _channel.BasicAckAsync(
            deliveryTag,
            multiple: false,
            cancellationToken);
    }

    public async Task RejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _channel.BasicRejectAsync(
            deliveryTag,
            requeue,
            cancellationToken);
    }

    public async Task NackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _channel.BasicNackAsync(
            deliveryTag,
            multiple: false,
            requeue,
            cancellationToken);
    }
}
