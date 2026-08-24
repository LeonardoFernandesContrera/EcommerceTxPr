using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.Domain.Entities;

public sealed class Order : BaseEntity
{
    private readonly List<OrderItem> _items = new();

    private Order()
    {
    }

    public Order(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Customer id must be supplied.",
                nameof(customerId));
        }

        CustomerId = customerId;
        Status = OrderStatus.Draft;
    }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal Total => _items.Sum(item => item.LineTotal);

    public void AddItem(
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        EnsureDraft();
        OrderItem.EnsureValid(productId, productName, unitPrice, quantity);

        var existingItem = _items.SingleOrDefault(
            item => item.ProductId == productId);

        if (existingItem is not null)
        {
            if (!string.Equals(
                    existingItem.ProductName,
                    productName,
                    StringComparison.Ordinal)
                || existingItem.UnitPrice != unitPrice)
            {
                throw new InvalidOperationException(
                    "Duplicate order items must use the same product snapshot.");
            }

            existingItem.IncreaseQuantity(quantity);
            return;
        }

        _items.Add(new OrderItem(productId, productName, unitPrice, quantity));
    }

    public void Place()
    {
        EnsureDraft();

        if (_items.Count == 0)
        {
            throw new InvalidOperationException(
                "An order must contain at least one item before it can be placed.");
        }

        Status = OrderStatus.Pending;
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft orders can be changed.");
        }
    }
}
