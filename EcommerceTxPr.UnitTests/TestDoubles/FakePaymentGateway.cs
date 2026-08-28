using EcommerceTxPr.Application.Payments.Gateways;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly Queue<PaymentGatewayResult> _results = new();

    public PaymentGatewayResult Result { get; set; } =
        PaymentGatewayResult.Succeeded("test-reference");

    public List<PaymentGatewayRequest> Requests { get; } = new();

    public Action<PaymentGatewayRequest>? OnProcess { get; set; }

    public void EnqueueResult(PaymentGatewayResult result)
    {
        _results.Enqueue(result);
    }

    public Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        OnProcess?.Invoke(request);
        var result = _results.Count > 0 ? _results.Dequeue() : Result;
        return Task.FromResult(result);
    }
}
