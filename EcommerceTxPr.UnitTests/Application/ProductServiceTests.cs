using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Products;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Application.Products.Services;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task GetAllAsync_returns_mapped_product_responses()
    {
        var product = new Product("SKU-001", "Product", 49.90m);
        var repository = new FakeProductRepository
        {
            GetAllResult = new[] { product }
        };
        var service = new ProductService(repository);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = Assert.Single(result.Value!);
        AssertMatches(product, response);
    }

    [Fact]
    public async Task CreateAsync_adds_product_and_returns_response()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);
        var request = new CreateProductRequest("SKU-001", "Product", 99m);

        var result = await service.CreateAsync(request, CancellationToken.None);

        var product = Assert.Single(repository.AddedProducts);
        Assert.Equal(request.Sku, product.Sku);
        Assert.Equal(request.Name, product.Name);
        Assert.Equal(request.Price, product.Price);
        Assert.True(result.IsSuccess);
        AssertMatches(product, result.Value!);
    }

    [Fact]
    public async Task CreateAsync_duplicate_sku_returns_conflict_without_adding()
    {
        var repository = new FakeProductRepository
        {
            GetBySkuResult = new Product("SKU-001", "Existing", 10m)
        };
        var service = new ProductService(repository);

        var result = await service.CreateAsync(
            new CreateProductRequest("SKU-001", "Duplicate", 20m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.DuplicateSku, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Empty(repository.AddedProducts);
    }

    [Fact]
    public async Task UpdateAsync_changes_details_without_changing_sku()
    {
        var product = new Product("STABLE-SKU", "Original", 10m);
        var repository = new FakeProductRepository { GetByIdResult = product };
        var service = new ProductService(repository);

        var result = await service.UpdateAsync(
            product.Id,
            new UpdateProductRequest("Updated", 20m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("STABLE-SKU", product.Sku);
        Assert.Equal("Updated", product.Name);
        Assert.Equal(20m, product.Price);
        Assert.Same(product, Assert.Single(repository.UpdatedProducts));
        Assert.Equal("STABLE-SKU", result.Value?.Sku);
    }

    [Fact]
    public async Task UpdateAsync_missing_product_returns_not_found_without_updating()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateProductRequest("Updated", 20m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Empty(repository.UpdatedProducts);
    }

    [Fact]
    public async Task DeleteAsync_existing_product_deactivates_and_updates()
    {
        var product = new Product("SKU-001", "Product", 10m);
        var repository = new FakeProductRepository { GetByIdResult = product };
        var service = new ProductService(repository);

        var result = await service.DeleteAsync(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value);
        Assert.False(product.IsActive);
        Assert.Same(product, Assert.Single(repository.UpdatedProducts));
    }

    [Fact]
    public async Task DeleteAsync_missing_product_returns_not_found_without_updating()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Empty(repository.UpdatedProducts);
    }

    private static void AssertMatches(Product product, ProductResponse response)
    {
        Assert.Equal(product.Id, response.Id);
        Assert.Equal(product.Sku, response.Sku);
        Assert.Equal(product.Name, response.Name);
        Assert.Equal(product.Price, response.Price);
        Assert.Equal(product.CreationDate, response.CreationDate);
    }
}
