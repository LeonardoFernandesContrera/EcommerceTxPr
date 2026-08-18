namespace EcommerceTxPr.Domain.Entities;

public sealed class Product : BaseEntity
{
    private Product()
    {
        Sku = string.Empty;
        Name = string.Empty;
    }

    public Product(string sku, string name, decimal price)
    {
        EnsureNotBlank(sku, nameof(sku));
        EnsureValidDetails(name, price);

        Sku = sku;
        Name = name;
        Price = price;
        IsActive = true;
    }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(string name, decimal price)
    {
        EnsureValidDetails(name, price);

        Name = name;
        Price = price;
    }

    public void Deactivate()
    {
        IsActive = false;
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
}
