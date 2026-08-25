using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly EcommerceTxPrDbContext _context;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public EfUnitOfWork(
        EcommerceTxPrDbContext context,
        IDatabaseErrorClassifier errorClassifier)
    {
        _context = context;
        _errorClassifier = errorClassifier;
    }

    public async Task<SaveChangesResult> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        var eventSnapshots = _context.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(owner => owner.DomainEvents.Count > 0)
            .Select(owner => (
                Owner: owner,
                Events: owner.DomainEvents.ToArray()))
            .ToArray();

        var outboxMessages = eventSnapshots
            .SelectMany(snapshot => snapshot.Events)
            .Select(DomainEventOutboxMapper.Map)
            .ToArray();

        _context.OutboxMessages.AddRange(outboxMessages);

        try
        {
            await _context
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var snapshot in eventSnapshots)
            {
                snapshot.Owner.ClearDomainEvents();
            }

            return SaveChangesResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return SaveChangesResult.ConcurrencyConflict;
        }
        catch (DbUpdateException exception)
        {
            var isIdempotencyConflict = _errorClassifier
                .IsIdempotencyConflict(exception);
            var isPaymentConflict = _errorClassifier
                .IsPaymentConflict(exception);

            _context.ChangeTracker.Clear();

            if (isIdempotencyConflict)
            {
                return SaveChangesResult.IdempotencyConflict;
            }

            if (isPaymentConflict)
            {
                return SaveChangesResult.PaymentConflict;
            }

            throw;
        }
    }
}
