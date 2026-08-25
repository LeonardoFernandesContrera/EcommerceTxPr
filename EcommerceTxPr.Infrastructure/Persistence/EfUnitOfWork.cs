using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Infrastructure.Context;
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
        try
        {
            await _context
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);

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
