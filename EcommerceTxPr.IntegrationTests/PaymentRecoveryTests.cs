using System.Net;
using System.Net.Http.Json;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentRecoveryTests
{
    [Fact]
    public async Task Pending_intent_is_durable_before_gateway_response_completes()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var orderId = await CreateOrderAsync(client);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        var gatewayReached = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGateway = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.BeforeReturningAsync = async (_, cancellationToken) =>
        {
            gatewayReached.TrySetResult(null);
            await releaseGateway.Task.WaitAsync(cancellationToken);
        };

        var processing = client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null);

        try
        {
            await gatewayReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var payment = await context.Payments
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OrderId == orderId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(OrderStatus.Pending, await context.Orders
                .Where(order => order.Id == orderId)
                .Select(order => order.Status)
                .SingleAsync());
            Assert.Empty(await context.OutboxMessages.ToListAsync());
        }
        finally
        {
            releaseGateway.TrySetResult(null);
        }

        using var response = await processing;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Lost_response_retry_reuses_provider_result_and_commits_once()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var orderId = await CreateOrderAsync(client);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        gateway.Result = PaymentGatewayResult.Succeeded("stored-success");
        gateway.EnqueueObservation(PaymentGatewayResult.Indeterminate());

        using var firstResponse = await client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null);
        using var retryResponse = await client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            firstResponse,
            HttpStatusCode.ServiceUnavailable,
            "Payment.OutcomeIndeterminate");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
        Assert.Single(gateway.Requests.Select(request => request.PaymentId)
            .Distinct());
        Assert.Single(gateway.Requests.Select(request => request.IdempotencyKey)
            .Distinct(StringComparer.Ordinal));
        await AssertSuccessfulStateAsync(factory, orderId, "stored-success");
    }

    [Fact]
    public async Task Final_save_failure_recovers_with_same_payment_and_provider_result()
    {
        var failure = new FailTerminalSaveOnce();
        using var factory = new CustomerApiFactory(services =>
        {
            services.AddSingleton(failure);
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, FailingTerminalUnitOfWork>();
        });
        using var client = factory.CreateClientWithDatabase();
        var orderId = await CreateOrderAsync(client);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        gateway.Result = PaymentGatewayResult.Succeeded("recoverable-success");
        failure.Enable();

        using var failedResponse = await client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            failedResponse.StatusCode);
        Guid persistedPaymentId;
        using (var pendingScope = factory.Services.CreateScope())
        {
            var context = pendingScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var payment = await context.Payments
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OrderId == orderId);
            persistedPaymentId = payment.Id;
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(OrderStatus.Pending, await context.Orders
                .Where(order => order.Id == orderId)
                .Select(order => order.Status)
                .SingleAsync());
            Assert.Empty(await context.OutboxMessages.ToListAsync());
        }

        using var retryResponse = await client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var recovered = await retryResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(recovered);
        Assert.Equal(persistedPaymentId, recovered.Id);
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
        Assert.All(
            gateway.Requests,
            request => Assert.Equal(persistedPaymentId, request.PaymentId));
        Assert.Single(gateway.Requests
            .Select(request => request.IdempotencyKey)
            .Distinct(StringComparer.Ordinal));
        await AssertSuccessfulStateAsync(
            factory,
            orderId,
            "recoverable-success");
    }

    private static async Task<Guid> CreateOrderAsync(HttpClient client)
    {
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);
        return order.Id;
    }

    private static async Task AssertSuccessfulStateAsync(
        CustomerApiFactory factory,
        Guid orderId,
        string providerReference)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var payment = await context.Payments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrderId == orderId);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(providerReference, payment.ProviderReference);
        Assert.Equal(OrderStatus.Paid, await context.Orders
            .Where(order => order.Id == orderId)
            .Select(order => order.Status)
            .SingleAsync());
        var outbox = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("payment.succeeded.v1", outbox.Type);
    }

    private sealed class FailTerminalSaveOnce
    {
        private int _enabled;
        private int _hasFailed;

        public void Enable()
        {
            Volatile.Write(ref _enabled, 1);
        }

        public bool TryFail(EcommerceTxPrDbContext context)
        {
            var hasTerminalPayment = context.ChangeTracker
                .Entries<Payment>()
                .Any(entry => entry.State == EntityState.Modified
                    && entry.Entity.Status != PaymentStatus.Pending);

            return Volatile.Read(ref _enabled) == 1
                && hasTerminalPayment
                && Interlocked.CompareExchange(ref _hasFailed, 1, 0) == 0;
        }
    }

    private sealed class FailingTerminalUnitOfWork : IUnitOfWork
    {
        private readonly EcommerceTxPrDbContext _context;
        private readonly IDatabaseErrorClassifier _errorClassifier;
        private readonly FailTerminalSaveOnce _failure;

        public FailingTerminalUnitOfWork(
            EcommerceTxPrDbContext context,
            IDatabaseErrorClassifier errorClassifier,
            FailTerminalSaveOnce failure)
        {
            _context = context;
            _errorClassifier = errorClassifier;
            _failure = failure;
        }

        public Task<SaveChangesResult> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            if (_failure.TryFail(_context))
            {
                _context.ChangeTracker.Clear();
                throw new DbUpdateException(
                    "Forced terminal payment persistence failure.");
            }

            return new EfUnitOfWork(_context, _errorClassifier)
                .SaveChangesAsync(cancellationToken);
        }
    }
}
