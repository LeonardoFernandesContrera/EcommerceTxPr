using EcommerceTxPr.Infrastructure.RabbitMq;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed class RecordingPaymentDeliveryAcknowledger
    : IPaymentDeliveryAcknowledger
{
    private readonly Func<bool>? _scopeDisposed;

    public RecordingPaymentDeliveryAcknowledger(
        Func<bool>? scopeDisposed = null)
    {
        _scopeDisposed = scopeDisposed;
    }

    public List<AcknowledgementCall> Calls { get; } = new();

    public Task AckAsync(
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        Calls.Add(new AcknowledgementCall(
            AcknowledgementKind.Ack,
            deliveryTag,
            false,
            _scopeDisposed?.Invoke() ?? false));
        return Task.CompletedTask;
    }

    public Task RejectAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        Calls.Add(new AcknowledgementCall(
            AcknowledgementKind.Reject,
            deliveryTag,
            requeue,
            _scopeDisposed?.Invoke() ?? false));
        return Task.CompletedTask;
    }

    public Task NackAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        Calls.Add(new AcknowledgementCall(
            AcknowledgementKind.Nack,
            deliveryTag,
            requeue,
            _scopeDisposed?.Invoke() ?? false));
        return Task.CompletedTask;
    }
}

internal sealed record AcknowledgementCall(
    AcknowledgementKind Kind,
    ulong DeliveryTag,
    bool Requeue,
    bool ScopeWasDisposed);

internal enum AcknowledgementKind
{
    Ack,
    Reject,
    Nack
}
