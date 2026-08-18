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
}
