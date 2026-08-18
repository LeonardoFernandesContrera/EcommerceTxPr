using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Products.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyCollection<Product>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Product?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task UpdateAsync(Product product, CancellationToken cancellationToken);
}
