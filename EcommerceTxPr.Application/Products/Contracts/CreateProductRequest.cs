using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Application.Products.Contracts;

public sealed record CreateProductRequest(
    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(50, ErrorMessage = "SKU must not exceed 50 characters.")]
    string Sku,
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
    string Name,
    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "Price must be greater than zero.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    decimal Price,
    [Range(0, int.MaxValue, ErrorMessage = "Initial stock cannot be negative.")]
    int StockQuantity);
