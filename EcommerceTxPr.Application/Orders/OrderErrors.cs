using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.Application.Orders;

public static class OrderErrors
{
    public static readonly Error NotFound = new(
        "Order.NotFound",
        "The order was not found.",
        ErrorType.NotFound);

    public static readonly Error Empty = new(
        "Order.Empty",
        "An order must contain at least one item.",
        ErrorType.Validation);

    public static readonly Error InvalidProduct = new(
        "Order.InvalidProduct",
        "Every order item must contain a valid product id.",
        ErrorType.Validation);

    public static readonly Error InvalidCustomer = new(
        "Order.InvalidCustomer",
        "A valid customer id is required.",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "Order.InvalidQuantity",
        "Order item quantity must be greater than zero.",
        ErrorType.Validation);

    public static readonly Error InventoryChanged = new(
        "Order.InventoryChanged",
        "Inventory changed while the order was being processed. Retry the operation.",
        ErrorType.Conflict);
}
