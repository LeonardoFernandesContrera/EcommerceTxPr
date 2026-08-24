namespace EcommerceTxPr.Application.Products.Contracts;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    DateTime CreationDate);
