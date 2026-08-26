using EcommerceTxPr.Infrastructure.Inbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class PaymentEventDeliveryHandler
    : IPaymentEventDeliveryHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentEventDeliveryHandler> _logger;

    public PaymentEventDeliveryHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentEventDeliveryHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(
        PaymentIntegrationEventDelivery delivery,
        ulong deliveryTag,
        IPaymentDeliveryAcknowledger acknowledger,
        CancellationToken cancellationToken)
    {
        PaymentIntegrationEventProcessingResult result;

        try
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var processor = scope.ServiceProvider
                    .GetRequiredService<IPaymentIntegrationEventProcessor>();
                result = await processor
                    .ProcessAsync(delivery, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Transient payment-event processing failure for MessageId "
                + "{MessageId} and Type {Type}; delivery will be requeued.",
                delivery.MessageId,
                delivery.Type);
            await acknowledger
                .NackAsync(deliveryTag, requeue: true, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        switch (result)
        {
            case PaymentIntegrationEventProcessingResult.Processed:
                _logger.LogInformation(
                    "Processed payment event {MessageId} of type {Type}.",
                    delivery.MessageId,
                    delivery.Type);
                await acknowledger
                    .AckAsync(deliveryTag, cancellationToken)
                    .ConfigureAwait(false);
                return;
            case PaymentIntegrationEventProcessingResult.Duplicate:
                _logger.LogInformation(
                    "Acknowledging duplicate payment event {MessageId} of "
                    + "type {Type}.",
                    delivery.MessageId,
                    delivery.Type);
                await acknowledger
                    .AckAsync(deliveryTag, cancellationToken)
                    .ConfigureAwait(false);
                return;
            case PaymentIntegrationEventProcessingResult.Poison:
                _logger.LogWarning(
                    "Rejecting poison payment event {MessageId} of type {Type} "
                    + "without requeue.",
                    delivery.MessageId,
                    delivery.Type);
                await acknowledger
                    .RejectAsync(deliveryTag, requeue: false, cancellationToken)
                    .ConfigureAwait(false);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported payment event processing result: {result}.");
        }
    }
}
