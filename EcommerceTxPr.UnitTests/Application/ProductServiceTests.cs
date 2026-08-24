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
        var product = new Product("SKU-001", "Product", 49.90m, 8);
        var repository = new FakeProductRepository
        {
            GetAllResult = new[] { product }
        };
        var service = new ProductService(repository, new FakeUnitOfWork());

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = Assert.Single(result.Value!);
        AssertMatches(product, response);
    }

    [Fact]
    public async Task CreateAsync_stages_product_commits_once_and_returns_stock()
    {
        var repository = new FakeProductRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(repository, unitOfWork);
        var request = new CreateProductRequest("SKU-001", "Product", 99m, 6);

        var result = await service.CreateAsync(request, CancellationToken.None);

        var product = Assert.Single(repository.AddedProducts);
        Assert.Equal(request.Sku, product.Sku);
        Assert.Equal(request.Name, product.Name);
        Assert.Equal(request.Price, product.Price);
        Assert.Equal(request.StockQuantity, product.StockQuantity);
        Assert.True(result.IsSuccess);
        AssertMatches(product, result.Value!);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_duplicate_sku_returns_conflict_without_adding_or_committing()
    {
        var repository = new FakeProductRepository
        {
            GetBySkuResult = new Product("SKU-001", "Existing", 10m, 1)
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(repository, unitOfWork);

        var result = await service.CreateAsync(
            new CreateProductRequest("SKU-001", "Duplicate", 20m, 2),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.DuplicateSku, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Empty(repository.AddedProducts);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_changes_details_preserves_sku_and_stock_and_commits_once()
    {
        var product = new Product("STABLE-SKU", "Original", 10m, 5);
        var repository = new FakeProductRepository { GetByIdResult = product };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(repository, unitOfWork);

        var result = await service.UpdateAsync(
            product.Id,
            new UpdateProductRequest("Updated", 20m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("STABLE-SKU", product.Sku);
        Assert.Equal("Updated", product.Name);
        Assert.Equal(20m, product.Price);
        Assert.Equal(5, product.StockQuantity);
        Assert.Equal("STABLE-SKU", result.Value?.Sku);
        Assert.Equal(5, result.Value?.StockQuantity);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_missing_product_returns_not_found_without_committing()
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(new FakeProductRepository(), unitOfWork);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateProductRequest("Updated", 20m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_concurrency_failure_returns_conflict()
    {
        var product = new Product("SKU-001", "Product", 10m, 1);
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var service = new ProductService(
            new FakeProductRepository { GetByIdResult = product },
            unitOfWork);

        var result = await service.UpdateAsync(
            product.Id,
            new UpdateProductRequest("Updated", 20m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.ConcurrentModification, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_existing_product_deactivates_and_commits_once()
    {
        var product = new Product("SKU-001", "Product", 10m, 1);
        var repository = new FakeProductRepository { GetByIdResult = product };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(repository, unitOfWork);

        var result = await service.DeleteAsync(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value);
        Assert.False(product.IsActive);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_missing_product_returns_not_found_without_committing()
    {
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(new FakeProductRepository(), unitOfWork);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_concurrency_failure_returns_conflict()
    {
        var product = new Product("SKU-001", "Product", 10m, 1);
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var service = new ProductService(
            new FakeProductRepository { GetByIdResult = product },
            unitOfWork);

        var result = await service.DeleteAsync(product.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.ConcurrentModification, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private static void AssertMatches(Product product, ProductResponse response)
    {
        Assert.Equal(product.Id, response.Id);
        Assert.Equal(product.Sku, response.Sku);
        Assert.Equal(product.Name, response.Name);
        Assert.Equal(product.Price, response.Price);
        Assert.Equal(product.StockQuantity, response.StockQuantity);
        Assert.Equal(product.CreationDate, response.CreationDate);
    }
}
