using System.Text.Json;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.Infrastructure.Outbox.Contracts;

namespace EcommerceTxPr.Infrastructure.Outbox;

public static class DomainEventOutboxMapper
{
    private const string PaymentSucceededType = "payment.succeeded.v1";
    private const string PaymentFailedType = "payment.failed.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static OutboxMessage Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return domainEvent switch
        {
            PaymentSucceededDomainEvent succeeded => MapSucceeded(succeeded),
            PaymentFailedDomainEvent failed => MapFailed(failed),
            _ => throw new InvalidOperationException(
                $"Unsupported domain event type: {domainEvent.GetType().Name}.")
        };
    }

    private static OutboxMessage MapSucceeded(
        PaymentSucceededDomainEvent domainEvent)
    {
        var payload = new PaymentSucceededV1Payload(
            domainEvent.PaymentId,
            domainEvent.OrderId,
            domainEvent.Amount,
            domainEvent.ProviderReference,
            domainEvent.OccurredOnUtc);

        return new OutboxMessage(
            PaymentSucceededType,
            JsonSerializer.Serialize(payload, SerializerOptions),
            domainEvent.OccurredOnUtc);
    }

    private static OutboxMessage MapFailed(
        PaymentFailedDomainEvent domainEvent)
    {
        var payload = new PaymentFailedV1Payload(
            domainEvent.PaymentId,
            domainEvent.OrderId,
            domainEvent.Amount,
            domainEvent.FailureCode,
            domainEvent.OccurredOnUtc);

        return new OutboxMessage(
            PaymentFailedType,
            JsonSerializer.Serialize(payload, SerializerOptions),
            domainEvent.OccurredOnUtc);
    }
}
