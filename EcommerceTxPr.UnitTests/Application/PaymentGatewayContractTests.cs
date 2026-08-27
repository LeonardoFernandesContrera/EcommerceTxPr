using EcommerceTxPr.Application.Payments.Gateways;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class PaymentGatewayContractTests
{
    [Fact]
    public void Provider_key_is_stable_bounded_and_derived_from_payment_identity()
    {
        var paymentId = Guid.Parse(
            "11111111-2222-3333-4444-555555555555");

        var first = PaymentProviderIdempotencyKey.Create(paymentId);
        var second = PaymentProviderIdempotencyKey.Create(paymentId);

        Assert.Equal(
            "payment-11111111222233334444555555555555",
            first);
        Assert.Equal(first, second);
        Assert.Equal(40, first.Length);
    }

    [Fact]
    public void Provider_key_distinguishes_payments_and_rejects_empty_identity()
    {
        var first = PaymentProviderIdempotencyKey.Create(Guid.Parse(
            "11111111-1111-1111-1111-111111111111"));
        var second = PaymentProviderIdempotencyKey.Create(Guid.Parse(
            "22222222-2222-2222-2222-222222222222"));

        Assert.NotEqual(first, second);
        Assert.Throws<ArgumentException>(
            () => PaymentProviderIdempotencyKey.Create(Guid.Empty));
    }

    [Fact]
    public void Indeterminate_result_has_no_terminal_provider_details()
    {
        var result = PaymentGatewayResult.Indeterminate();

        Assert.Equal(PaymentGatewayStatus.Indeterminate, result.Status);
        Assert.Null(result.ProviderReference);
        Assert.Null(result.FailureCode);
    }
}
