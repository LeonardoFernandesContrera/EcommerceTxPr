using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Domain.Events;

namespace EcommerceTxPr.Domain.Entities;

public sealed class Payment : BaseEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    private Payment()
    {
    }

    public Payment(Guid orderId, decimal amount)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order id must be supplied.",
                nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be greater than zero.");
        }

        OrderId = orderId;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    IReadOnlyCollection<IDomainEvent> IHasDomainEvents.DomainEvents =>
        _domainEvents.AsReadOnly();

    public void MarkSucceeded(string providerReference)
    {
        EnsurePending();
        EnsureNotBlank(providerReference, nameof(providerReference));

        ProviderReference = providerReference;
        FailureCode = null;
        Status = PaymentStatus.Succeeded;
        _domainEvents.Add(new PaymentSucceededDomainEvent(
            Id,
            OrderId,
            Amount,
            providerReference,
            DateTime.UtcNow));
    }

    public void MarkFailed(string failureCode)
    {
        EnsurePending();
        EnsureNotBlank(failureCode, nameof(failureCode));

        ProviderReference = null;
        FailureCode = failureCode;
        Status = PaymentStatus.Failed;
        _domainEvents.Add(new PaymentFailedDomainEvent(
            Id,
            OrderId,
            Amount,
            failureCode,
            DateTime.UtcNow));
    }

    void IHasDomainEvents.ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending payments can be completed.");
        }
    }

    private static void EnsureNotBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be null or blank.",
                parameterName);
        }
    }
}
