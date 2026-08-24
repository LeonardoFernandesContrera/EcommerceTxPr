using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.Application.Products;

public static class ProductErrors
{
    public static readonly Error NotFound = new(
        "Product.NotFound",
        "The product was not found.",
        ErrorType.NotFound);

    public static readonly Error DuplicateSku = new(
        "Product.DuplicateSku",
        "A product with the supplied SKU already exists.",
        ErrorType.Conflict);

    public static readonly Error InsufficientStock = new(
        "Product.InsufficientStock",
        "The available product stock cannot fulfill the requested quantity.",
        ErrorType.Conflict);

    public static readonly Error ConcurrentModification = new(
        "Product.ConcurrentModification",
        "The product changed while it was being updated. Retry the operation.",
        ErrorType.Conflict);
}
