namespace EcommerceTxPr.Infrastructure.Outbox.Contracts;

internal sealed record PaymentSucceededV1Payload(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string ProviderReference,
    DateTime OccurredOnUtc);
