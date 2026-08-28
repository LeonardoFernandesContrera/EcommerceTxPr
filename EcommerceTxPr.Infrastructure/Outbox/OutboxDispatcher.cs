using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.Infrastructure.Outbox;

public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly EcommerceTxPrDbContext _context;
    private readonly IOutboxMessagePublisher _publisher;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        EcommerceTxPrDbContext context,
        IOutboxMessagePublisher publisher,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _context = context;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OutboxDispatchResult> DispatchBatchAsync(
        CancellationToken cancellationToken)
    {
        var messages = await _context.OutboxMessages
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.OccurredOnUtc)
            .ThenBy(message => message.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            return new OutboxDispatchResult(0, 0, 0);
        }

        var publishedCount = 0;
        var failedCount = 0;

        foreach (var message in messages)
        {
            try
            {
                await _publisher
                    .PublishAsync(message, cancellationToken)
                    .ConfigureAwait(false);
                message.MarkProcessed(DateTime.UtcNow);
                publishedCount++;
                _logger.LogInformation(
                    "Published Outbox message {OutboxMessageId} of type {Type}.",
                    message.Id,
                    message.Type);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OutboxPublicationException exception)
            {
                RecordFailure(message, exception.Category, exception);
                failedCount++;
                break;
            }
            catch (Exception exception)
            {
                RecordFailure(
                    message,
                    OutboxPublicationFailureCategory.Publish,
                    exception);
                failedCount++;
                break;
            }
        }

        await _context
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return new OutboxDispatchResult(
            messages.Count,
            publishedCount,
            failedCount);
    }

    private void RecordFailure(
        OutboxMessage message,
        OutboxPublicationFailureCategory category,
        Exception exception)
    {
        message.RecordFailure(
            $"RabbitMQ publication failed ({category}).");
        _logger.LogWarning(
            exception,
            "Failed to publish Outbox message {OutboxMessageId} of type {Type}. "
            + "Failure category: {FailureCategory}.",
            message.Id,
            message.Type,
            category);
    }
}
