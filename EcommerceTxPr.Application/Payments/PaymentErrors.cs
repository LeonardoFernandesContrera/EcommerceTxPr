using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.Application.Payments;

public static class PaymentErrors
{
    public static readonly Error NotFound = new(
        "Payment.NotFound",
        "The payment was not found.",
        ErrorType.NotFound);

    public static readonly Error OrderAlreadyPaid = new(
        "Payment.OrderAlreadyPaid",
        "The order has already been paid.",
        ErrorType.Conflict);

    public static readonly Error OrderNotPayable = new(
        "Payment.OrderNotPayable",
        "Only pending orders can be paid.",
        ErrorType.Conflict);

    public static readonly Error ConcurrentModification = new(
        "Payment.ConcurrentModification",
        "The payment or order changed while payment was being persisted.",
        ErrorType.Conflict);

    public static readonly Error OutcomeIndeterminate = new(
        "Payment.OutcomeIndeterminate",
        "The payment outcome is temporarily unavailable. Retry the operation.",
        ErrorType.Unavailable);
}
