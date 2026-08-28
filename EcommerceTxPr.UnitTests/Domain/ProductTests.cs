using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Constructor_creates_valid_active_product()
    {
        var beforeCreation = DateTime.UtcNow;

        var product = new Product("SKU-001", "Mechanical Keyboard", 249.90m, 12);

        var afterCreation = DateTime.UtcNow;
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal(249.90m, product.Price);
        Assert.Equal(12, product.StockQuantity);
        Assert.True(product.IsActive);
        Assert.NotEqual(Guid.Empty, product.Version);
        Assert.Equal(DateTimeKind.Utc, product.CreationDate.Kind);
        Assert.InRange(product.CreationDate, beforeCreation, afterCreation);
    }

    [Fact]
    public void UpdateDetails_changes_name_and_price_without_changing_sku_or_identity()
    {
        var product = new Product("SKU-001", "Original Name", 100m, 0);
        var originalId = product.Id;
        var originalCreationDate = product.CreationDate;

        product.UpdateDetails("Updated Name", 150m);

        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal("Updated Name", product.Name);
        Assert.Equal(150m, product.Price);
        Assert.Equal(originalId, product.Id);
        Assert.Equal(originalCreationDate, product.CreationDate);
    }

    [Fact]
    public void Deactivate_marks_product_inactive()
    {
        var product = new Product("SKU-001", "Product", 10m, 0);

        product.Deactivate();

        Assert.False(product.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_null_or_blank_sku(string? sku)
    {
        Assert.Throws<ArgumentException>(
            () => new Product(sku!, "Product", 10m, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_null_or_blank_name(string? name)
    {
        Assert.Throws<ArgumentException>(
            () => new Product("SKU-001", name!, 10m, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_price(decimal price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Product("SKU-001", "Product", price, 0));
    }

    [Fact]
    public void Constructor_accepts_zero_initial_stock()
    {
        var product = new Product("SKU-001", "Product", 10m, 0);

        Assert.Equal(0, product.StockQuantity);
    }

    [Fact]
    public void Constructor_rejects_negative_initial_stock()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Product("SKU-001", "Product", 10m, -1));
    }

    [Fact]
    public void IncreaseStock_adds_quantity()
    {
        var product = new Product("SKU-001", "Product", 10m, 2);

        product.IncreaseStock(3);

        Assert.Equal(5, product.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IncreaseStock_rejects_non_positive_quantity(int quantity)
    {
        var product = new Product("SKU-001", "Product", 10m, 2);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => product.IncreaseStock(quantity));
        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_reduces_quantity()
    {
        var product = new Product("SKU-001", "Product", 10m, 5);

        product.DecreaseStock(3);

        Assert.Equal(2, product.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DecreaseStock_rejects_non_positive_quantity(int quantity)
    {
        var product = new Product("SKU-001", "Product", 10m, 2);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => product.DecreaseStock(quantity));
        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_rejects_quantity_above_stock_without_going_negative()
    {
        var product = new Product("SKU-001", "Product", 10m, 2);

        Assert.Throws<InvalidOperationException>(() => product.DecreaseStock(3));

        Assert.Equal(2, product.StockQuantity);
        Assert.True(product.StockQuantity >= 0);
    }

    [Fact]
    public void Version_changes_after_each_persisted_product_mutation()
    {
        var product = new Product("SKU-001", "Product", 10m, 5);
        var originalVersion = product.Version;

        product.UpdateDetails("Updated", 20m);
        var detailsVersion = product.Version;
        product.IncreaseStock(1);
        var increaseVersion = product.Version;
        product.DecreaseStock(1);
        var decreaseVersion = product.Version;
        product.Deactivate();

        Assert.NotEqual(originalVersion, detailsVersion);
        Assert.NotEqual(detailsVersion, increaseVersion);
        Assert.NotEqual(increaseVersion, decreaseVersion);
        Assert.NotEqual(decreaseVersion, product.Version);
    }
}
