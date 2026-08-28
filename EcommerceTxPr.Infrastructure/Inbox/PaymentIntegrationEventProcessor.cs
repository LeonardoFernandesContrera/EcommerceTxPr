using System.Text.Json;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.Outbox.Contracts;
using EcommerceTxPr.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Inbox;

internal sealed class PaymentIntegrationEventProcessor
    : IPaymentIntegrationEventProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    private readonly EcommerceTxPrDbContext _context;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public PaymentIntegrationEventProcessor(
        EcommerceTxPrDbContext context,
        IDatabaseErrorClassifier errorClassifier)
    {
        _context = context;
        _errorClassifier = errorClassifier;
    }

    public async Task<PaymentIntegrationEventProcessingResult> ProcessAsync(
        PaymentIntegrationEventDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        if (!Guid.TryParse(delivery.MessageId, out var messageId)
            || messageId == Guid.Empty
            || !IsSupportedAndConsistentType(
                delivery.Type,
                delivery.RoutingKey))
        {
            return PaymentIntegrationEventProcessingResult.Poison;
        }

        var existingType = await GetInboxTypeAsync(
                messageId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existingType is not null)
        {
            return ClassifyExistingIdentity(existingType, delivery.Type!);
        }

        var processedOnUtc = DateTime.UtcNow;
        var projection = TryCreateProjection(
            messageId,
            delivery,
            processedOnUtc);

        if (projection is null)
        {
            return PaymentIntegrationEventProcessingResult.Poison;
        }

        _context.InboxMessages.Add(new InboxMessage(
            messageId,
            delivery.Type!,
            processedOnUtc));
        _context.PaymentEventProjections.Add(projection);

        try
        {
            await _context
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
            return PaymentIntegrationEventProcessingResult.Processed;
        }
        catch (DbUpdateException exception)
        {
            var isInboxConflict = _errorClassifier
                .IsInboxConflict(exception);

            if (!isInboxConflict)
            {
                throw;
            }

            _context.ChangeTracker.Clear();

            var winningType = await GetInboxTypeAsync(
                    messageId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (winningType is null)
            {
                throw;
            }

            return ClassifyExistingIdentity(winningType, delivery.Type!);
        }
    }

    private async Task<string?> GetInboxTypeAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await _context.InboxMessages
            .AsNoTracking()
            .Where(message => message.MessageId == messageId)
            .Select(message => message.Type)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static PaymentIntegrationEventProcessingResult
        ClassifyExistingIdentity(string storedType, string incomingType)
    {
        return string.Equals(
            storedType,
            incomingType,
            StringComparison.Ordinal)
            ? PaymentIntegrationEventProcessingResult.Duplicate
            : PaymentIntegrationEventProcessingResult.Poison;
    }

    private static bool IsSupportedAndConsistentType(
        string? type,
        string routingKey)
    {
        var isSupported = type is OutboxMessageTypes.PaymentSucceededV1
            or OutboxMessageTypes.PaymentFailedV1;

        return isSupported
            && string.Equals(type, routingKey, StringComparison.Ordinal);
    }

    private static PaymentEventProjection? TryCreateProjection(
        Guid messageId,
        PaymentIntegrationEventDelivery delivery,
        DateTime processedOnUtc)
    {
        try
        {
            return delivery.Type switch
            {
                OutboxMessageTypes.PaymentSucceededV1 =>
                    TryCreateSucceededProjection(
                        messageId,
                        JsonSerializer.Deserialize<PaymentSucceededV1Payload>(
                            delivery.Body,
                            SerializerOptions),
                        processedOnUtc),
                OutboxMessageTypes.PaymentFailedV1 =>
                    TryCreateFailedProjection(
                        messageId,
                        JsonSerializer.Deserialize<PaymentFailedV1Payload>(
                            delivery.Body,
                            SerializerOptions),
                        processedOnUtc),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static PaymentEventProjection? TryCreateSucceededProjection(
        Guid messageId,
        PaymentSucceededV1Payload? payload,
        DateTime processedOnUtc)
    {
        if (payload is null
            || !IsValidCommonPayload(
                payload.PaymentId,
                payload.OrderId,
                payload.Amount,
                payload.OccurredOnUtc)
            || !IsValidRequiredText(
                payload.ProviderReference,
                PaymentEventProjection.MaxProviderReferenceLength))
        {
            return null;
        }

        return PaymentEventProjection.Succeeded(
            messageId,
            payload.PaymentId,
            payload.OrderId,
            payload.Amount,
            payload.ProviderReference,
            payload.OccurredOnUtc,
            processedOnUtc);
    }

    private static PaymentEventProjection? TryCreateFailedProjection(
        Guid messageId,
        PaymentFailedV1Payload? payload,
        DateTime processedOnUtc)
    {
        if (payload is null
            || !IsValidCommonPayload(
                payload.PaymentId,
                payload.OrderId,
                payload.Amount,
                payload.OccurredOnUtc)
            || !IsValidRequiredText(
                payload.FailureCode,
                PaymentEventProjection.MaxFailureCodeLength))
        {
            return null;
        }

        return PaymentEventProjection.Failed(
            messageId,
            payload.PaymentId,
            payload.OrderId,
            payload.Amount,
            payload.FailureCode,
            payload.OccurredOnUtc,
            processedOnUtc);
    }

    private static bool IsValidCommonPayload(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        DateTime occurredOnUtc)
    {
        return paymentId != Guid.Empty
            && orderId != Guid.Empty
            && amount > 0
            && occurredOnUtc.Kind == DateTimeKind.Utc;
    }

    private static bool IsValidRequiredText(string? value, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= maxLength;
    }
}
