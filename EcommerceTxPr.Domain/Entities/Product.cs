namespace EcommerceTxPr.Domain.Entities;

public sealed class Product : BaseEntity
{
    private Product()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }

    public Product(string sku, string name, decimal price, int stockQuantity)
    {
        EnsureNotBlank(sku, nameof(sku));
        EnsureValidDetails(name, price);

        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stockQuantity),
                "Initial stock quantity cannot be negative.");
        }

        Sku = sku;
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        Version = Guid.NewGuid();
    }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public Guid Version { get; private set; }

    public void UpdateDetails(string name, decimal price)
    {
        EnsureValidDetails(name, price);

        Name = name;
        Price = price;
        RefreshVersion();
    }

    public void Deactivate()
    {
        IsActive = false;
        RefreshVersion();
    }

    public void IncreaseStock(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        StockQuantity = checked(StockQuantity + quantity);
        RefreshVersion();
    }

    public void DecreaseStock(int quantity)
    {
        EnsurePositiveQuantity(quantity);

        if (quantity > StockQuantity)
        {
            throw new InvalidOperationException(
                "The requested quantity exceeds the available stock.");
        }

        StockQuantity -= quantity;
        RefreshVersion();
    }

    private static void EnsureValidDetails(string name, decimal price)
    {
        EnsureNotBlank(name, nameof(name));

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                "Product price must be greater than zero.");
        }
    }

    private static void EnsureNotBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be null or blank.",
                parameterName);
        }
    }

    private static void EnsurePositiveQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Stock quantity must be greater than zero.");
        }
    }

    private void RefreshVersion()
    {
        Version = Guid.NewGuid();
    }
}
