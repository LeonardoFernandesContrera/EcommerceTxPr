using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Products.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<IReadOnlyCollection<ProductResponse>, Error>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var products = await _productRepository
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyCollection<ProductResponse> response = products
            .Select(ToResponse)
            .ToArray();

        return Result<IReadOnlyCollection<ProductResponse>, Error>.Success(response);
    }

    public async Task<Result<ProductResponse, Error>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return product is null
            ? Result<ProductResponse, Error>.Failure(ProductErrors.NotFound)
            : Result<ProductResponse, Error>.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse, Error>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var existingProduct = await _productRepository
            .GetBySkuAsync(request.Sku, cancellationToken)
            .ConfigureAwait(false);

        if (existingProduct is not null)
        {
            return Result<ProductResponse, Error>.Failure(ProductErrors.DuplicateSku);
        }

        var product = new Product(request.Sku, request.Name, request.Price);

        await _productRepository
            .AddAsync(product, cancellationToken)
            .ConfigureAwait(false);

        return Result<ProductResponse, Error>.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse, Error>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
        {
            return Result<ProductResponse, Error>.Failure(ProductErrors.NotFound);
        }

        product.UpdateDetails(request.Name, request.Price);

        await _productRepository
            .UpdateAsync(product, cancellationToken)
            .ConfigureAwait(false);

        return Result<ProductResponse, Error>.Success(ToResponse(product));
    }

    public async Task<Result<Guid, Error>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
        {
            return Result<Guid, Error>.Failure(ProductErrors.NotFound);
        }

        product.Deactivate();

        await _productRepository
            .UpdateAsync(product, cancellationToken)
            .ConfigureAwait(false);

        return Result<Guid, Error>.Success(product.Id);
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Price,
            product.CreationDate);
    }
}
