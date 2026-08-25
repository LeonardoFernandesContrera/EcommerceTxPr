using EcommerceTxPr.Application.Payments.Gateways;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakePaymentGateway : IPaymentGateway
{
    public PaymentGatewayResult Result { get; set; } =
        PaymentGatewayResult.Succeeded("test-reference");

    public List<PaymentGatewayRequest> Requests { get; } = new();

    public Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Result);
    }
}
