using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories;

public sealed class OrderIdempotencyRepository : IOrderIdempotencyRepository
{
    private readonly EcommerceTxPrDbContext _context;

    public OrderIdempotencyRepository(EcommerceTxPrDbContext context)
    {
        _context = context;
    }

    public async Task<OrderIdempotencyRecord?> GetByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken)
    {
        return await _context.OrderIdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.KeyHash == keyHash,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        OrderIdempotencyRecord record,
        CancellationToken cancellationToken)
    {
        await _context.OrderIdempotencyRecords
            .AddAsync(record, cancellationToken)
            .ConfigureAwait(false);
    }
}
