using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed record PublishedOutboxMessage(
    Guid Id,
    string Type,
    string Payload);

internal sealed class DeterministicOutboxMessagePublisher
    : IOutboxMessagePublisher
{
    private readonly Queue<OutboxPublicationException?> _results = new();

    public List<PublishedOutboxMessage> Requests { get; } = new();

    public void EnqueueSuccess()
    {
        _results.Enqueue(null);
    }

    public void EnqueueFailure(OutboxPublicationFailureCategory category)
    {
        _results.Enqueue(new OutboxPublicationException(
            category,
            new InvalidOperationException("test-only publication failure")));
    }

    public Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(new PublishedOutboxMessage(
            message.Id,
            message.Type,
            message.Payload));
        var result = _results.Count == 0 ? null : _results.Dequeue();

        return result is null
            ? Task.CompletedTask
            : Task.FromException(result);
    }
}
