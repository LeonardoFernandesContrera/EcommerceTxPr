namespace EcommerceTxPr.Infrastructure.Outbox;

public sealed record OutboxDispatchResult(
    int LoadedCount,
    int PublishedCount,
    int FailedCount);
