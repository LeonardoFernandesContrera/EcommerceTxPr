using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.Application.Payments.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentResponse, Error>> ProcessPaymentAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository
            .GetByIdForPaymentAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<PaymentResponse, Error>.Failure(OrderErrors.NotFound);
        }

        if (order.Status == OrderStatus.Paid)
        {
            return Result<PaymentResponse, Error>.Failure(
                PaymentErrors.OrderAlreadyPaid);
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result<PaymentResponse, Error>.Failure(
                PaymentErrors.OrderNotPayable);
        }

        var existingPayment = await _paymentRepository
            .GetByOrderIdAsync(order.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingPayment is not null)
        {
            return Result<PaymentResponse, Error>.Failure(
                PaymentErrors.AlreadyExists);
        }

        var payment = new Payment(order.Id, order.Total);
        var gatewayResult = await _paymentGateway
            .ProcessAsync(
                new PaymentGatewayRequest(payment.Id, payment.Amount),
                cancellationToken)
            .ConfigureAwait(false);

        switch (gatewayResult.Status)
        {
            case PaymentGatewayStatus.Succeeded:
                payment.MarkSucceeded(gatewayResult.ProviderReference!);
                order.MarkPaid();
                break;
            case PaymentGatewayStatus.Failed:
                payment.MarkFailed(gatewayResult.FailureCode!);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported payment gateway status: {gatewayResult.Status}.");
        }

        await _paymentRepository
            .AddAsync(payment, cancellationToken)
            .ConfigureAwait(false);

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        return saveResult switch
        {
            SaveChangesResult.Success =>
                Result<PaymentResponse, Error>.Success(ToResponse(payment)),
            SaveChangesResult.PaymentConflict =>
                Result<PaymentResponse, Error>.Failure(
                    PaymentErrors.AlreadyExists),
            SaveChangesResult.ConcurrencyConflict =>
                Result<PaymentResponse, Error>.Failure(
                    PaymentErrors.ConcurrentModification),
            _ => throw new InvalidOperationException(
                $"Unsupported save result: {saveResult}.")
        };
    }

    public async Task<Result<PaymentResponse, Error>> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository
            .GetByOrderIdAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        return payment is null
            ? Result<PaymentResponse, Error>.Failure(PaymentErrors.NotFound)
            : Result<PaymentResponse, Error>.Success(ToResponse(payment));
    }

    private static PaymentResponse ToResponse(Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Status,
            EnsureUtc(payment.CreationDate),
            payment.ProviderReference,
            payment.FailureCode);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
