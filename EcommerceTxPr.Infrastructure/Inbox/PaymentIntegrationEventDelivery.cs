namespace EcommerceTxPr.Infrastructure.Inbox;

public sealed record PaymentIntegrationEventDelivery(
    string? MessageId,
    string? Type,
    string RoutingKey,
    byte[] Body);
