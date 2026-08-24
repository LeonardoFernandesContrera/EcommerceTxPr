using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeProductRepository : IProductRepository
{
    public IReadOnlyCollection<Product> GetAllResult { get; set; } =
        Array.Empty<Product>();

    public Product? GetByIdResult { get; set; }

    public Product? GetBySkuResult { get; set; }

    public IReadOnlyCollection<Product> GetByIdsResult { get; set; } =
        Array.Empty<Product>();

    public List<IReadOnlyCollection<Guid>> GetByIdsRequests { get; } = new();

    public List<Product> AddedProducts { get; } = new();

    public Task<IReadOnlyCollection<Product>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(GetAllResult);
    }

    public Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(GetByIdResult);
    }

    public Task<Product?> GetBySkuAsync(
        string sku,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(GetBySkuResult);
    }

    public Task<IReadOnlyCollection<Product>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        GetByIdsRequests.Add(ids.ToArray());
        return Task.FromResult(GetByIdsResult);
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        AddedProducts.Add(product);
        return Task.CompletedTask;
    }

}
