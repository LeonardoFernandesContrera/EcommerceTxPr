namespace EcommerceTxPr.Application.Payments.Gateways;

public sealed record PaymentGatewayRequest(
    Guid PaymentId,
    decimal Amount);
