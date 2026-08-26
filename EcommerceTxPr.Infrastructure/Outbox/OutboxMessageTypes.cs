namespace EcommerceTxPr.Infrastructure.Outbox;

public static class OutboxMessageTypes
{
    public const string PaymentSucceededV1 = "payment.succeeded.v1";

    public const string PaymentFailedV1 = "payment.failed.v1";
}
