using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Payments;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Application.Payments.Services;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ProcessPaymentAsync_missing_order_returns_not_found_without_side_effects()
    {
        var orderRepository = new FakeOrderRepository();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            orderRepository,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.NotFound, result.Error);
        Assert.Empty(paymentRepository.GetByOrderIdRequests);
        Assert.Empty(paymentRepository.AddedPayments);
        Assert.Empty(gateway.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_paid_order_returns_conflict_without_calling_gateway()
    {
        var order = CreatePlacedOrder();
        order.MarkPaid();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.OrderAlreadyPaid, result.Error);
        Assert.Empty(paymentRepository.GetByOrderIdRequests);
        Assert.Empty(paymentRepository.AddedPayments);
        Assert.Empty(gateway.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_draft_order_returns_not_payable_without_calling_gateway()
    {
        var order = new Order(Guid.NewGuid());
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.OrderNotPayable, result.Error);
        Assert.Empty(paymentRepository.GetByOrderIdRequests);
        Assert.Empty(gateway.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_existing_pending_payment_is_resumed()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository
        {
            GetByOrderIdForProcessingResult = new Payment(order.Id, order.Total)
        };
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Resumed, result.Value?.Status);
        Assert.Empty(paymentRepository.AddedPayments);
        Assert.Single(gateway.Requests);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_success_uses_order_total_marks_paid_and_commits_twice()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Succeeded("provider-123")
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Total, result.Value.Payment.Amount);
        Assert.Equal(PaymentStatus.Succeeded, result.Value.Payment.Status);
        Assert.Equal("provider-123", result.Value.Payment.ProviderReference);
        Assert.Null(result.Value.Payment.FailureCode);
        Assert.Equal(OrderStatus.Paid, order.Status);
        var payment = Assert.Single(paymentRepository.AddedPayments);
        Assert.Equal(order.Total, payment.Amount);
        var gatewayRequest = Assert.Single(gateway.Requests);
        Assert.Equal(payment.Id, gatewayRequest.PaymentId);
        Assert.Equal(order.Total, gatewayRequest.Amount);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_gateway_failure_persists_failed_payment_and_keeps_order_pending()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Failed("Declined")
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Failed, result.Value.Payment.Status);
        Assert.Equal("Declined", result.Value.Payment.FailureCode);
        Assert.Null(result.Value.Payment.ProviderReference);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(
            PaymentStatus.Failed,
            Assert.Single(paymentRepository.AddedPayments).Status);
        Assert.Single(gateway.Requests);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ProcessPaymentAsync_initial_concurrency_conflict_does_not_call_gateway()
    {
        var order = CreatePlacedOrder();
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            new FakePaymentRepository(),
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Payment.ConcurrentModification", result.Error?.Code);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Empty(gateway.Requests);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(SaveChangesResult.IdempotencyConflict)]
    [InlineData((SaveChangesResult)999)]
    public async Task ProcessPaymentAsync_unsupported_save_result_fails_closed(
        SaveChangesResult saveResult)
    {
        var order = CreatePlacedOrder();
        var unitOfWork = new FakeUnitOfWork { Result = saveResult };
        var service = new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            new FakePaymentRepository(),
            new FakePaymentGateway(),
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessPaymentAsync(
                order.Id,
                CancellationToken.None));

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task GetByOrderIdAsync_existing_payment_returns_response_without_side_effects()
    {
        var payment = new Payment(Guid.NewGuid(), 25m);
        payment.MarkFailed("Declined");
        var paymentRepository = new FakePaymentRepository
        {
            GetByOrderIdResult = payment
        };
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = new PaymentService(
            new FakeOrderRepository(),
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.GetByOrderIdAsync(
            payment.OrderId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.Value?.Id);
        Assert.Equal(payment.Amount, result.Value?.Amount);
        Assert.Equal(PaymentStatus.Failed, result.Value?.Status);
        Assert.Equal("Declined", result.Value?.FailureCode);
        Assert.Empty(gateway.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task GetByOrderIdAsync_missing_payment_returns_not_found()
    {
        var service = new PaymentService(
            new FakeOrderRepository(),
            new FakePaymentRepository(),
            new FakePaymentGateway(),
            new FakeUnitOfWork());

        var result = await service.GetByOrderIdAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.NotFound, result.Error);
    }

    private static Order CreatePlacedOrder()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "First", 12.50m, 2);
        order.AddItem(Guid.NewGuid(), "Second", 5m, 3);
        order.Place();
        return order;
    }
}
