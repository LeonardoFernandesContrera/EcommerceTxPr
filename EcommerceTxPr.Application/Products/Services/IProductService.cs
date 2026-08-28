using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Products.Contracts;

namespace EcommerceTxPr.Application.Products.Services;

public interface IProductService
{
    Task<Result<IReadOnlyCollection<ProductResponse>, Error>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<Result<ProductResponse, Error>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<Result<ProductResponse, Error>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<Result<ProductResponse, Error>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken);

    Task<Result<Guid, Error>> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken);
}
