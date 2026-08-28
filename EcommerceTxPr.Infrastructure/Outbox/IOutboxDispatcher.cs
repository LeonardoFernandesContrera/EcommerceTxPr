namespace EcommerceTxPr.Infrastructure.Outbox;

internal interface IOutboxDispatcher
{
    Task<OutboxDispatchResult> DispatchBatchAsync(
        CancellationToken cancellationToken);
}
