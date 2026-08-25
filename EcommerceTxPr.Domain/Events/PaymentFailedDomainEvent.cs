namespace EcommerceTxPr.Domain.Events;

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string FailureCode,
    DateTime OccurredOnUtc) : IDomainEvent;
