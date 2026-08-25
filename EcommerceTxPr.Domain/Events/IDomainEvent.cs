namespace EcommerceTxPr.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
