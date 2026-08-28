namespace EcommerceTxPr.Domain.Entities;

public sealed class OrderItem
{
    private OrderItem()
    {
        ProductName = string.Empty;
    }

    internal OrderItem(
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        EnsureValid(productId, productName, unitPrice, quantity);

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Order item quantity must be greater than zero.");
        }

        Quantity = checked(Quantity + quantity);
    }

    internal static void EnsureValid(
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Product id must be supplied.",
                nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException(
                "Product name cannot be null or blank.",
                nameof(productName));
        }

        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Order item unit price must be greater than zero.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Order item quantity must be greater than zero.");
        }
    }
}
