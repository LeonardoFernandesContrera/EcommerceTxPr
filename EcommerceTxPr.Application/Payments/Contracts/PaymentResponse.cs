using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.Application.Payments.Contracts;

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    PaymentStatus Status,
    DateTime CreationDate,
    string? ProviderReference,
    string? FailureCode);
