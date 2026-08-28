using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Products.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
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

        var product = new Product(
            request.Sku,
            request.Name,
            request.Price,
            request.StockQuantity);

        await _productRepository
            .AddAsync(product, cancellationToken)
            .ConfigureAwait(false);

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return Result<ProductResponse, Error>.Failure(
                ProductErrors.ConcurrentModification);
        }

        EnsureSuccessfulSave(saveResult);

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

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return Result<ProductResponse, Error>.Failure(
                ProductErrors.ConcurrentModification);
        }

        EnsureSuccessfulSave(saveResult);

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

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return Result<Guid, Error>.Failure(
                ProductErrors.ConcurrentModification);
        }

        EnsureSuccessfulSave(saveResult);

        return Result<Guid, Error>.Success(product.Id);
    }

    private static void EnsureSuccessfulSave(SaveChangesResult saveResult)
    {
        if (saveResult != SaveChangesResult.Success)
        {
            throw new InvalidOperationException(
                $"Unsupported save result: {saveResult}.");
        }
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Sku,
            product.Name,
            product.Price,
            product.StockQuantity,
            product.CreationDate);
    }
}
