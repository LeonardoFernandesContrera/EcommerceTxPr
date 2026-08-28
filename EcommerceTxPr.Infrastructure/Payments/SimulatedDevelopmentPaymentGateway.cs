using EcommerceTxPr.Application.Payments.Gateways;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.Infrastructure.Payments;

public sealed class SimulatedDevelopmentPaymentGateway : IPaymentGateway
{
    private readonly SimulatedPaymentOutcome _outcome;

    public SimulatedDevelopmentPaymentGateway(
        IOptions<SimulatedDevelopmentPaymentGatewayOptions> options)
    {
        _outcome = options.Value.GetOutcome();
    }

    public Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        var result = _outcome switch
        {
            SimulatedPaymentOutcome.Succeeded =>
                PaymentGatewayResult.Succeeded(
                    $"simulated-{request.PaymentId:N}"),
            SimulatedPaymentOutcome.Failed =>
                PaymentGatewayResult.Failed("SimulatedDecline"),
            _ => throw new InvalidOperationException(
                $"Unsupported simulated payment outcome: {_outcome}.")
        };

        return Task.FromResult(result);
    }
}
