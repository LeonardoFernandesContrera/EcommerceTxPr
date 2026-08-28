namespace EcommerceTxPr.Application.Payments.Contracts;

public sealed record PaymentProcessingResponse(
    PaymentResponse Payment,
    PaymentProcessingStatus Status);
