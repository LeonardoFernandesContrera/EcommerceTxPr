using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly EcommerceTxPrDbContext _context;

    public OrderRepository(EcommerceTxPrDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        await _context.Orders
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
