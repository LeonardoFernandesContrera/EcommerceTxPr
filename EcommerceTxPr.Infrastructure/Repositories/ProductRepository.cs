using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly EcommerceTxPrDbContext _context;

    public ProductRepository(EcommerceTxPrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Product>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .FirstOrDefaultAsync(
                product => product.Id == id && product.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Product?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Sku == sku,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await _context.Products
            .AsNoTracking()
            .Where(product => ids.Contains(product.Id) && product.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        await _context.Products
            .AddAsync(product, cancellationToken)
            .ConfigureAwait(false);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        if (_context.Entry(product).State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                "A product must be loaded by this repository before it can be updated.");
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
