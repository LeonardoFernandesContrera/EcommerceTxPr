using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Payments.Contracts;

namespace EcommerceTxPr.Application.Payments.Services;

public interface IPaymentService
{
    Task<Result<PaymentProcessingResponse, Error>> ProcessPaymentAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<Result<PaymentResponse, Error>> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);
}
