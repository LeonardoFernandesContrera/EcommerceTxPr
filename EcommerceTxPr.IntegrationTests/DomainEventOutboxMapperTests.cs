using System.Text.Json;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.Infrastructure.Outbox;

namespace EcommerceTxPr.IntegrationTests;

public sealed class DomainEventOutboxMapperTests
{
    private static readonly Guid PaymentId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111");

    private static readonly Guid OrderId = Guid.Parse(
        "22222222-2222-2222-2222-222222222222");

    private static readonly DateTime OccurredOnUtc = new(
        2026,
        8,
        25,
        12,
        30,
        0,
        DateTimeKind.Utc);

    [Fact]
    public void Payment_succeeded_maps_to_stable_v1_payload()
    {
        var domainEvent = new PaymentSucceededDomainEvent(
            PaymentId,
            OrderId,
            25.50m,
            "provider-reference",
            OccurredOnUtc);

        var message = DomainEventOutboxMapper.Map(domainEvent);

        Assert.Equal(OutboxMessageTypes.PaymentSucceededV1, message.Type);
        Assert.Equal(OccurredOnUtc, message.OccurredOnUtc);
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        Assert.Equal(
            new[]
            {
                "paymentId",
                "orderId",
                "amount",
                "providerReference",
                "occurredOnUtc"
            },
            payload.EnumerateObject().Select(property => property.Name));
        Assert.Equal(PaymentId, payload.GetProperty("paymentId").GetGuid());
        Assert.Equal(OrderId, payload.GetProperty("orderId").GetGuid());
        Assert.Equal(25.50m, payload.GetProperty("amount").GetDecimal());
        Assert.Equal(
            "provider-reference",
            payload.GetProperty("providerReference").GetString());
        Assert.Equal(
            OccurredOnUtc,
            payload.GetProperty("occurredOnUtc").GetDateTime());
    }

    [Fact]
    public void Payment_failed_maps_to_stable_v1_payload()
    {
        var domainEvent = new PaymentFailedDomainEvent(
            PaymentId,
            OrderId,
            25.50m,
            "CardDeclined",
            OccurredOnUtc);

        var message = DomainEventOutboxMapper.Map(domainEvent);

        Assert.Equal(OutboxMessageTypes.PaymentFailedV1, message.Type);
        Assert.Equal(OccurredOnUtc, message.OccurredOnUtc);
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        Assert.Equal(
            new[]
            {
                "paymentId",
                "orderId",
                "amount",
                "failureCode",
                "occurredOnUtc"
            },
            payload.EnumerateObject().Select(property => property.Name));
        Assert.Equal(PaymentId, payload.GetProperty("paymentId").GetGuid());
        Assert.Equal(OrderId, payload.GetProperty("orderId").GetGuid());
        Assert.Equal(25.50m, payload.GetProperty("amount").GetDecimal());
        Assert.Equal(
            "CardDeclined",
            payload.GetProperty("failureCode").GetString());
        Assert.Equal(
            OccurredOnUtc,
            payload.GetProperty("occurredOnUtc").GetDateTime());
    }

    [Fact]
    public void Unknown_domain_event_fails_closed()
    {
        var domainEvent = new UnknownDomainEvent(OccurredOnUtc);

        var exception = Assert.Throws<InvalidOperationException>(
            () => DomainEventOutboxMapper.Map(domainEvent));

        Assert.Contains(nameof(UnknownDomainEvent), exception.Message);
    }

    private sealed record UnknownDomainEvent(DateTime OccurredOnUtc)
        : IDomainEvent;
}
