using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Payments;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Application.Payments.Services;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class PaymentDurableIntentServiceTests
{
    [Fact]
    public async Task New_success_commits_pending_before_gateway_then_commits_terminal()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Succeeded("provider-123")
        };
        var unitOfWork = new FakeUnitOfWork();
        var operations = new List<string>();
        unitOfWork.OnSaveChanges = call => operations.Add(
            $"save-{call}-{Assert.Single(paymentRepository.AddedPayments).Status}");
        gateway.OnProcess = _ => operations.Add("gateway");
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Created, result.Value?.Status);
        Assert.Equal(
            new[] { "save-1-Pending", "gateway", "save-2-Succeeded" },
            operations);
        Assert.Equal(OrderStatus.Paid, order.Status);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(
            $"payment-{request.PaymentId:N}",
            request.IdempotencyKey);
        Assert.Equal(order.Total, request.Amount);
    }

    [Fact]
    public async Task Failed_initial_pending_commit_never_calls_gateway()
    {
        var order = CreatePlacedOrder();
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        unitOfWork.EnqueueResult(SaveChangesResult.ConcurrencyConflict);
        var service = CreateService(
            order,
            new FakePaymentRepository(),
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.ConcurrentModification, result.Error);
        Assert.Empty(gateway.Requests);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Definitive_failure_uses_pending_then_terminal_save_and_keeps_order_pending()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Failed("Declined")
        };
        var unitOfWork = new FakeUnitOfWork();
        var statusesAtSave = new List<PaymentStatus>();
        unitOfWork.OnSaveChanges = _ => statusesAtSave.Add(
            Assert.Single(paymentRepository.AddedPayments).Status);
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Created, result.Value?.Status);
        Assert.Equal(
            new[] { PaymentStatus.Pending, PaymentStatus.Failed },
            statusesAtSave);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Indeterminate_outcome_leaves_committed_intent_pending_without_final_save()
    {
        var order = CreatePlacedOrder();
        var paymentRepository = new FakePaymentRepository();
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Indeterminate()
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.OutcomeIndeterminate, result.Error);
        Assert.Equal(ErrorType.Unavailable, result.Error?.Type);
        var payment = Assert.Single(paymentRepository.AddedPayments);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Empty(((IHasDomainEvents)payment).DomainEvents);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Single(gateway.Requests);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Existing_pending_resumes_same_payment_and_provider_key()
    {
        var order = CreatePlacedOrder();
        var payment = new Payment(order.Id, order.Total);
        var paymentRepository = new FakePaymentRepository
        {
            GetByOrderIdForProcessingResult = payment
        };
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Resumed, result.Value?.Status);
        Assert.Empty(paymentRepository.AddedPayments);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(payment.Id, request.PaymentId);
        Assert.Equal($"payment-{payment.Id:N}", request.IdempotencyKey);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded)]
    [InlineData(PaymentStatus.Failed)]
    public async Task Existing_terminal_payment_replays_without_gateway_or_save(
        PaymentStatus paymentStatus)
    {
        var order = CreatePlacedOrder();
        var payment = new Payment(order.Id, order.Total);

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            payment.MarkSucceeded("persisted-reference");
            order.MarkPaid();
        }
        else
        {
            payment.MarkFailed("PersistedDecline");
        }

        var paymentRepository = new FakePaymentRepository
        {
            GetByOrderIdForProcessingResult = payment
        };
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Replayed, result.Value?.Status);
        Assert.Equal(payment.Id, result.Value?.Payment.Id);
        Assert.Empty(gateway.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Initial_payment_conflict_discards_candidate_and_uses_winner_identity()
    {
        var order = CreatePlacedOrder();
        var winningPayment = new Payment(order.Id, order.Total);
        var paymentRepository = new FakePaymentRepository();
        paymentRepository.EnqueueProcessingResult(null);
        paymentRepository.EnqueueProcessingResult(winningPayment);
        var orderRepository = new FakeOrderRepository();
        orderRepository.EnqueueGetByIdForPaymentResult(order);
        orderRepository.EnqueueGetByIdForPaymentResult(order);
        var gateway = new FakePaymentGateway();
        var unitOfWork = new FakeUnitOfWork();
        unitOfWork.EnqueueResult(SaveChangesResult.PaymentConflict);
        unitOfWork.EnqueueResult(SaveChangesResult.Success);
        var service = new PaymentService(
            orderRepository,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Resumed, result.Value?.Status);
        var losingPayment = Assert.Single(paymentRepository.AddedPayments);
        Assert.NotEqual(losingPayment.Id, winningPayment.Id);
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(winningPayment.Id, request.PaymentId);
        Assert.Equal(
            $"payment-{winningPayment.Id:N}",
            request.IdempotencyKey);
        Assert.DoesNotContain(
            gateway.Requests,
            candidate => candidate.PaymentId == losingPayment.Id);
    }

    [Fact]
    public async Task Terminal_concurrency_reloads_and_returns_persisted_success_pair()
    {
        var order = CreatePlacedOrder();
        var losingPayment = new Payment(order.Id, order.Total);
        var persistedPayment = new Payment(order.Id, order.Total);
        persistedPayment.MarkSucceeded("winning-reference");
        var paymentRepository = new FakePaymentRepository();
        paymentRepository.EnqueueProcessingResult(losingPayment);
        paymentRepository.EnqueueProcessingResult(persistedPayment);
        var orderRepository = new FakeOrderRepository
        {
            GetByIdForPaymentResult = order
        };
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var service = new PaymentService(
            orderRepository,
            paymentRepository,
            new FakePaymentGateway(),
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentProcessingStatus.Replayed, result.Value?.Status);
        Assert.Equal(persistedPayment.Id, result.Value?.Payment.Id);
        Assert.Equal("winning-reference", result.Value?.Payment.ProviderReference);
        Assert.Equal(2, orderRepository.GetByIdForPaymentRequests.Count);
        Assert.Equal(
            2,
            paymentRepository.GetByOrderIdForProcessingRequests.Count);
    }

    [Fact]
    public async Task Terminal_concurrency_with_persisted_pending_pair_returns_retryable_conflict()
    {
        var order = CreatePlacedOrder();
        var losingPayment = new Payment(order.Id, order.Total);
        var persistedPending = new Payment(order.Id, order.Total);
        var paymentRepository = new FakePaymentRepository();
        paymentRepository.EnqueueProcessingResult(losingPayment);
        paymentRepository.EnqueueProcessingResult(persistedPending);
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var gateway = new FakePaymentGateway
        {
            Result = PaymentGatewayResult.Failed("LosingDecline")
        };
        var service = CreateService(
            order,
            paymentRepository,
            gateway,
            unitOfWork);

        var result = await service.ProcessPaymentAsync(
            order.Id,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentErrors.ConcurrentModification, result.Error);
        Assert.Equal(PaymentStatus.Pending, persistedPending.Status);
        Assert.Single(gateway.Requests);
    }

    [Fact]
    public async Task Inconsistent_terminal_pair_fails_closed()
    {
        var order = CreatePlacedOrder();
        var payment = new Payment(order.Id, order.Total);
        payment.MarkSucceeded("provider-reference");
        var service = CreateService(
            order,
            new FakePaymentRepository
            {
                GetByOrderIdForProcessingResult = payment
            },
            new FakePaymentGateway(),
            new FakeUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessPaymentAsync(
                order.Id,
                CancellationToken.None));
    }

    private static PaymentService CreateService(
        Order order,
        FakePaymentRepository paymentRepository,
        FakePaymentGateway gateway,
        FakeUnitOfWork unitOfWork)
    {
        return new PaymentService(
            new FakeOrderRepository { GetByIdForPaymentResult = order },
            paymentRepository,
            gateway,
            unitOfWork);
    }

    private static Order CreatePlacedOrder()
    {
        var order = new Order(Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "Product", 25m, 1);
        order.Place();
        return order;
    }
}
