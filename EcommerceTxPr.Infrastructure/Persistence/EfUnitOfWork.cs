using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly EcommerceTxPrDbContext _context;

    public EfUnitOfWork(EcommerceTxPrDbContext context)
    {
        _context = context;
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
            return SaveChangesResult.ConcurrencyConflict;
        }
    }
}
