namespace EcommerceTxPr.Domain.Events;

public sealed record PaymentSucceededDomainEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string ProviderReference,
    DateTime OccurredOnUtc) : IDomainEvent;
