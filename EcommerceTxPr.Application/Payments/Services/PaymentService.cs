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

    public async Task<Result<PaymentProcessingResponse, Error>> ProcessPaymentAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository
            .GetByIdForPaymentAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<PaymentProcessingResponse, Error>.Failure(
                OrderErrors.NotFound);
        }

        var payment = await _paymentRepository
            .GetByOrderIdForProcessingAsync(order.Id, cancellationToken)
            .ConfigureAwait(false);

        if (payment is not null)
        {
            return await ContinueExistingAsync(
                    order,
                    payment,
                    PaymentProcessingStatus.Resumed,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var orderError = GetOrderWithoutPaymentError(order);

        if (orderError is not null)
        {
            return Result<PaymentProcessingResponse, Error>.Failure(orderError);
        }

        payment = new Payment(order.Id, order.Total);
        await _paymentRepository
            .AddAsync(payment, cancellationToken)
            .ConfigureAwait(false);
        var initialSaveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (initialSaveResult == SaveChangesResult.PaymentConflict)
        {
            var winningState = await ReloadStateAsync(
                    orderId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (winningState.Payment is null)
            {
                throw new InvalidOperationException(
                    "A payment conflict was reported without a persisted "
                    + "winning payment.");
            }

            return await ContinueExistingAsync(
                    winningState.Order,
                    winningState.Payment,
                    PaymentProcessingStatus.Resumed,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (initialSaveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return Result<PaymentProcessingResponse, Error>.Failure(
                PaymentErrors.ConcurrentModification);
        }

        EnsureSuccessfulSave(initialSaveResult, "pending payment");

        return await ProcessPendingAsync(
                order,
                payment,
                PaymentProcessingStatus.Created,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<PaymentProcessingResponse, Error>>
        ContinueExistingAsync(
            Order order,
            Payment payment,
            PaymentProcessingStatus pendingStatus,
            CancellationToken cancellationToken)
    {
        EnsurePaymentBelongsToOrder(payment, order);

        return (payment.Status, order.Status) switch
        {
            (PaymentStatus.Pending, OrderStatus.Pending) =>
                await ProcessPendingAsync(
                        order,
                        payment,
                        pendingStatus,
                        cancellationToken)
                    .ConfigureAwait(false),
            (PaymentStatus.Succeeded, OrderStatus.Paid) =>
                SuccessfulProcessing(
                    payment,
                    PaymentProcessingStatus.Replayed),
            (PaymentStatus.Failed, OrderStatus.Pending) =>
                SuccessfulProcessing(
                    payment,
                    PaymentProcessingStatus.Replayed),
            _ => throw InconsistentState(payment, order)
        };
    }

    private async Task<Result<PaymentProcessingResponse, Error>>
        ProcessPendingAsync(
            Order order,
            Payment payment,
            PaymentProcessingStatus processingStatus,
            CancellationToken cancellationToken)
    {
        var gatewayResult = await _paymentGateway
            .ProcessAsync(
                new PaymentGatewayRequest(
                    payment.Id,
                    payment.Amount,
                    PaymentProviderIdempotencyKey.Create(payment.Id)),
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
            case PaymentGatewayStatus.Indeterminate:
                return Result<PaymentProcessingResponse, Error>.Failure(
                    PaymentErrors.OutcomeIndeterminate);
            default:
                throw new InvalidOperationException(
                    $"Unsupported payment gateway status: {gatewayResult.Status}.");
        }

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult == SaveChangesResult.Success)
        {
            return SuccessfulProcessing(payment, processingStatus);
        }

        if (saveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return await ReconcileTerminalConflictAsync(
                    order.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Unsupported terminal payment save result: {saveResult}.");
    }

    private async Task<Result<PaymentProcessingResponse, Error>>
        ReconcileTerminalConflictAsync(
            Guid orderId,
            CancellationToken cancellationToken)
    {
        var persisted = await ReloadStateAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (persisted.Payment is null)
        {
            throw new InvalidOperationException(
                "A terminal payment concurrency conflict was reported for a "
                + "missing payment.");
        }

        EnsurePaymentBelongsToOrder(persisted.Payment, persisted.Order);

        return (persisted.Payment.Status, persisted.Order.Status) switch
        {
            (PaymentStatus.Succeeded, OrderStatus.Paid) =>
                SuccessfulProcessing(
                    persisted.Payment,
                    PaymentProcessingStatus.Replayed),
            (PaymentStatus.Failed, OrderStatus.Pending) =>
                SuccessfulProcessing(
                    persisted.Payment,
                    PaymentProcessingStatus.Replayed),
            (PaymentStatus.Pending, OrderStatus.Pending) =>
                Result<PaymentProcessingResponse, Error>.Failure(
                    PaymentErrors.ConcurrentModification),
            _ => throw InconsistentState(persisted.Payment, persisted.Order)
        };
    }

    private async Task<(Order Order, Payment? Payment)> ReloadStateAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository
            .GetByIdForPaymentAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            throw new InvalidOperationException(
                "A payment operation references a missing order.");
        }

        var payment = await _paymentRepository
            .GetByOrderIdForProcessingAsync(orderId, cancellationToken)
            .ConfigureAwait(false);
        return (order, payment);
    }

    private static Error? GetOrderWithoutPaymentError(Order order)
    {
        return order.Status switch
        {
            OrderStatus.Pending => null,
            OrderStatus.Paid => PaymentErrors.OrderAlreadyPaid,
            _ => PaymentErrors.OrderNotPayable
        };
    }

    private static void EnsurePaymentBelongsToOrder(
        Payment payment,
        Order order)
    {
        if (payment.OrderId != order.Id || payment.Amount != order.Total)
        {
            throw new InvalidOperationException(
                "The persisted payment does not match its order.");
        }
    }

    private static InvalidOperationException InconsistentState(
        Payment payment,
        Order order)
    {
        return new InvalidOperationException(
            "Inconsistent persisted payment and order state: "
            + $"Payment={payment.Status}, Order={order.Status}.");
    }

    private static void EnsureSuccessfulSave(
        SaveChangesResult saveResult,
        string operation)
    {
        if (saveResult != SaveChangesResult.Success)
        {
            throw new InvalidOperationException(
                $"Unsupported {operation} save result: {saveResult}.");
        }
    }

    private static Result<PaymentProcessingResponse, Error>
        SuccessfulProcessing(
            Payment payment,
            PaymentProcessingStatus status)
    {
        return Result<PaymentProcessingResponse, Error>.Success(
            new PaymentProcessingResponse(ToResponse(payment), status));
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
