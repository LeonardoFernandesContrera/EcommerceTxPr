namespace EcommerceTxPr.Infrastructure.Outbox.Contracts;

internal sealed record PaymentFailedV1Payload(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string FailureCode,
    DateTime OccurredOnUtc);
