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

    public static readonly Error IdempotencyKeyRequired = new(
        "Order.IdempotencyKeyRequired",
        "An Idempotency-Key header is required.",
        ErrorType.Validation);

    public static readonly Error IdempotencyKeyTooLong = new(
        "Order.IdempotencyKeyTooLong",
        "The Idempotency-Key header must not exceed 100 characters.",
        ErrorType.Validation);

    public static readonly Error IdempotencyKeyInvalid = new(
        "Order.IdempotencyKeyInvalid",
        "Exactly one Idempotency-Key header value must be supplied.",
        ErrorType.Validation);

    public static readonly Error IdempotencyKeyConflict = new(
        "Order.IdempotencyKeyConflict",
        "The Idempotency-Key has already been used for another order request.",
        ErrorType.Conflict);

    public static readonly Error InventoryChanged = new(
        "Order.InventoryChanged",
        "Inventory changed while the order was being processed. Retry the operation.",
        ErrorType.Conflict);
}
