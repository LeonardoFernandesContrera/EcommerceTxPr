namespace EcommerceTxPr.Application.Payments.Gateways;

public static class PaymentProviderIdempotencyKey
{
    public static string Create(Guid paymentId)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Payment id must be supplied.",
                nameof(paymentId));
        }

        return $"payment-{paymentId:N}";
    }
}
