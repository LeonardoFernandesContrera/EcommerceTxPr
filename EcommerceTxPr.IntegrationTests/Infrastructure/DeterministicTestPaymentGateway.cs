using EcommerceTxPr.Application.Payments.Gateways;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed class DeterministicTestPaymentGateway : IPaymentGateway
{
    public PaymentGatewayResult Result { get; set; } =
        PaymentGatewayResult.Succeeded("test-provider-reference");

    public List<PaymentGatewayRequest> Requests { get; } = new();

    public Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Result);
    }
}
