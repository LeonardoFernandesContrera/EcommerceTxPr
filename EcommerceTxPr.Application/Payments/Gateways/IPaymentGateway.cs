namespace EcommerceTxPr.Application.Payments.Gateways;

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken);
}
