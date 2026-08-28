using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.UnitTests.Domain;

public sealed class OrderTests
{
    [Fact]
    public void Constructor_creates_empty_draft_order()
    {
        var customerId = Guid.NewGuid();

        var order = new Order(customerId);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(order.Items);
        Assert.Equal(0m, order.Total);
    }

    [Fact]
    public void AddItem_adds_product_snapshot()
    {
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Snapshot Name", 25m, 2);

        var item = Assert.Single(order.Items);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Snapshot Name", item.ProductName);
        Assert.Equal(25m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(50m, item.LineTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_rejects_non_positive_quantity(int quantity)
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => order.AddItem(Guid.NewGuid(), "Product", 10m, quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_rejects_non_positive_price(decimal price)
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => order.AddItem(Guid.NewGuid(), "Product", price, 1));
    }

    [Fact]
    public void AddItem_rejects_empty_product_id()
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<ArgumentException>(
            () => order.AddItem(Guid.Empty, "Product", 10m, 1));
    }

    [Fact]
    public void AddItem_rejects_blank_product_name()
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<ArgumentException>(
            () => order.AddItem(Guid.NewGuid(), "   ", 10m, 1));
    }

    [Fact]
    public void AddItem_merges_duplicate_product_quantity()
    {
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Product", 10m, 2);
        order.AddItem(productId, "Product", 10m, 3);

        var item = Assert.Single(order.Items);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(50m, item.LineTotal);
    }

    [Fact]
    public void AddItem_revalidates_snapshot_when_product_already_exists()
    {
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Product", 10m, 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => order.AddItem(productId, "Product", 0m, 1));
        Assert.Throws<ArgumentException>(
            () => order.AddItem(productId, "   ", 10m, 1));

        Assert.Equal(1, Assert.Single(order.Items).Quantity);
    }

    [Fact]
    public void AddItem_rejects_conflicting_duplicate_product_snapshot()
    {
        var order = new Order(Guid.NewGuid());
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Original", 10m, 2);

        Assert.Throws<InvalidOperationException>(
            () => order.AddItem(productId, "Changed", 10m, 1));
        Assert.Throws<InvalidOperationException>(
            () => order.AddItem(productId, "Original", 11m, 1));

        var item = Assert.Single(order.Items);
        Assert.Equal("Original", item.ProductName);
        Assert.Equal(10m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Total_is_calculated_from_order_items()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "First", 12.50m, 2);
        order.AddItem(Guid.NewGuid(), "Second", 5m, 3);

        Assert.Equal(40m, order.Total);
    }

    [Fact]
    public void Place_rejects_empty_order()
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => order.Place());
        Assert.Equal(OrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Place_changes_status_without_changing_identity()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10m, 1);
        var originalId = order.Id;
        var originalCreationDate = order.CreationDate;

        order.Place();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(originalId, order.Id);
        Assert.Equal(originalCreationDate, order.CreationDate);
    }

    [Fact]
    public void AddItem_rejects_changes_after_order_is_placed()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10m, 1);
        order.Place();

        Assert.Throws<InvalidOperationException>(
            () => order.AddItem(Guid.NewGuid(), "Another", 20m, 1));
    }

    [Fact]
    public void MarkPaid_changes_pending_order_to_paid()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10m, 1);
        order.Place();

        order.MarkPaid();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkPaid_rejects_draft_order()
    {
        var order = new Order(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => order.MarkPaid());
        Assert.Equal(OrderStatus.Draft, order.Status);
    }

    [Fact]
    public void MarkPaid_rejects_paid_order()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 10m, 1);
        order.Place();
        order.MarkPaid();

        Assert.Throws<InvalidOperationException>(() => order.MarkPaid());
        Assert.Equal(OrderStatus.Paid, order.Status);
    }
}
