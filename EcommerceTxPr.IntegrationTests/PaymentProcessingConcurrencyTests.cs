using System.Net;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Sdk;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentProcessingConcurrencyTests
{
    [Fact]
    public async Task Concurrent_first_creation_uses_only_winning_payment_at_gateway()
    {
        var creationRace = new ConcurrentCreationRace();
        using var factory = new CustomerApiFactory(services =>
        {
            services.AddSingleton(creationRace);
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, ConcurrentCreationUnitOfWork>();
        });
        using var client = factory.CreateClientWithDatabase();
        var orderId = await CreateOrderAsync(client);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        var responseGate = ConfigureSerializedResponses(gateway);
        creationRace.Enable();
        using var testDeadline = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));
        var cancellationToken = testDeadline.Token;

        var firstRequest = client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null,
            cancellationToken);
        var secondRequest = client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null,
            cancellationToken);

        try
        {
            await WaitForSecondGatewayRequestAsync(
                responseGate,
                firstRequest,
                secondRequest,
                cancellationToken);
            var firstCompleted = await Task.WhenAny(firstRequest, secondRequest)
                .WaitAsync(cancellationToken);
            var firstResponse = await firstCompleted;
            Assert.Contains(
                firstResponse.StatusCode,
                new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        }
        finally
        {
            responseGate.ReleaseSecond.TrySetResult(null);
        }

        using var firstFinalResponse = await firstRequest.WaitAsync(
            cancellationToken);
        using var secondFinalResponse = await secondRequest.WaitAsync(
            cancellationToken);
        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.Created },
            new[]
            {
                firstFinalResponse.StatusCode,
                secondFinalResponse.StatusCode
            }.OrderBy(status => status));
        Assert.Equal(2, creationRace.CandidatePaymentIds.Count);
        Assert.Equal(2, creationRace.CandidatePaymentIds.Distinct().Count());
        Assert.NotEqual(Guid.Empty, creationRace.WinningPaymentId);
        Assert.All(
            gateway.Requests,
            request => Assert.Equal(
                creationRace.WinningPaymentId,
                request.PaymentId));
        var losingPaymentId = Assert.Single(
            creationRace.CandidatePaymentIds,
            paymentId => paymentId != creationRace.WinningPaymentId);
        Assert.DoesNotContain(
            gateway.Requests,
            request => request.PaymentId == losingPaymentId);
        Assert.Single(gateway.Requests
            .Select(request => request.IdempotencyKey)
            .Distinct(StringComparer.Ordinal));
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
        await AssertSingleSuccessfulCommitAsync(
            factory,
            orderId,
            creationRace.WinningPaymentId);
    }

    [Fact]
    public async Task Concurrent_resume_uses_same_provider_key_and_one_terminal_commit()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var orderId = await CreateOrderAsync(client);
        var payment = await SeedPendingPaymentAsync(factory, orderId);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        var responseGate = ConfigureSerializedResponses(gateway);
        using var testDeadline = new CancellationTokenSource(
            TimeSpan.FromSeconds(30));
        var cancellationToken = testDeadline.Token;

        var firstRequest = client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null,
            cancellationToken);
        var secondRequest = client.PostAsync(
            $"/api/orders/{orderId}/payments",
            content: null,
            cancellationToken);

        try
        {
            await WaitForSecondGatewayRequestAsync(
                responseGate,
                firstRequest,
                secondRequest,
                cancellationToken);
            var firstCompleted = await Task.WhenAny(firstRequest, secondRequest)
                .WaitAsync(cancellationToken);
            var firstResponse = await firstCompleted;
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }
        finally
        {
            responseGate.ReleaseSecond.TrySetResult(null);
        }

        using var firstFinalResponse = await firstRequest.WaitAsync(
            cancellationToken);
        using var secondFinalResponse = await secondRequest.WaitAsync(
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstFinalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondFinalResponse.StatusCode);
        Assert.All(
            gateway.Requests,
            request => Assert.Equal(payment.Id, request.PaymentId));
        Assert.Single(gateway.Requests
            .Select(request => request.IdempotencyKey)
            .Distinct(StringComparer.Ordinal));
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
        await AssertSingleSuccessfulCommitAsync(factory, orderId, payment.Id);
    }

    private static async Task WaitForSecondGatewayRequestAsync(
        ResponseGate responseGate,
        Task<HttpResponseMessage> firstRequest,
        Task<HttpResponseMessage> secondRequest,
        CancellationToken cancellationToken)
    {
        var gatewayRequest = responseGate.SecondGatewayRequest.Task;
        var completed = await Task.WhenAny(
                gatewayRequest,
                firstRequest,
                secondRequest)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (completed == gatewayRequest || gatewayRequest.IsCompleted)
        {
            await gatewayRequest.ConfigureAwait(false);
            return;
        }

        var requestName = completed == firstRequest ? "first" : "second";
        var request = completed == firstRequest ? firstRequest : secondRequest;
        using var response = await request.ConfigureAwait(false);
        var responseBody = await response.Content
            .ReadAsStringAsync()
            .ConfigureAwait(false);

        throw new XunitException(
            $"The {requestName} payment request completed before the second "
            + $"gateway request arrived. HTTP {(int)response.StatusCode} "
            + $"({response.StatusCode}). Body: {responseBody}");
    }

    private static ResponseGate ConfigureSerializedResponses(
        DeterministicTestPaymentGateway gateway)
    {
        var gate = new ResponseGate();
        var arrivals = 0;
        gateway.BeforeReturningAsync = async (_, cancellationToken) =>
        {
            var arrival = Interlocked.Increment(ref arrivals);

            if (arrival == 1)
            {
                await gate.SecondGatewayRequest.Task.WaitAsync(
                    cancellationToken);
                return;
            }

            if (arrival == 2)
            {
                gate.SecondGatewayRequest.TrySetResult(null);
                await gate.ReleaseSecond.Task.WaitAsync(cancellationToken);
                return;
            }

            throw new InvalidOperationException(
                "Only two gateway requests were expected.");
        };
        return gate;
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

    private static async Task<Payment> SeedPendingPaymentAsync(
        CustomerApiFactory factory,
        Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var order = await context.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == orderId);
        var payment = new Payment(orderId, order.Total);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment;
    }

    private static async Task AssertSingleSuccessfulCommitAsync(
        CustomerApiFactory factory,
        Guid orderId,
        Guid paymentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var payment = await context.Payments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrderId == orderId);
        Assert.Equal(paymentId, payment.Id);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(OrderStatus.Paid, await context.Orders
            .Where(order => order.Id == orderId)
            .Select(order => order.Status)
            .SingleAsync());
        var outbox = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("payment.succeeded.v1", outbox.Type);
    }

    private sealed class ResponseGate
    {
        public TaskCompletionSource<object?> SecondGatewayRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> ReleaseSecond { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ConcurrentCreationRace
    {
        private readonly object _sync = new();
        private readonly List<Guid> _candidatePaymentIds = new();
        private readonly TaskCompletionSource<object?> _firstCandidateReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> _winnerCommitted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        private int _enabled;

        public IReadOnlyList<Guid> CandidatePaymentIds
        {
            get
            {
                lock (_sync)
                {
                    return _candidatePaymentIds.ToArray();
                }
            }
        }

        public Guid WinningPaymentId { get; private set; }

        public void Enable()
        {
            Volatile.Write(ref _enabled, 1);
        }

        public bool IsEnabled => Volatile.Read(ref _enabled) == 1;

        public int Register(Guid candidatePaymentId)
        {
            lock (_sync)
            {
                _candidatePaymentIds.Add(candidatePaymentId);
            }

            var arrival = Interlocked.Increment(ref _arrivals);

            if (arrival == 1)
            {
                _firstCandidateReady.TrySetResult(null);
            }

            return arrival;
        }

        public Task WaitForFirstCandidateAsync(
            CancellationToken cancellationToken)
        {
            return _firstCandidateReady.Task.WaitAsync(cancellationToken);
        }

        public Task WaitForWinnerAsync(CancellationToken cancellationToken)
        {
            return _winnerCommitted.Task.WaitAsync(cancellationToken);
        }

        public void CompleteWinner(Guid paymentId)
        {
            WinningPaymentId = paymentId;
            _winnerCommitted.TrySetResult(null);
        }

        public void FailWinner(Exception exception)
        {
            _winnerCommitted.TrySetException(exception);
        }
    }

    private sealed class ConcurrentCreationUnitOfWork : IUnitOfWork
    {
        private readonly EcommerceTxPrDbContext _context;
        private readonly IDatabaseErrorClassifier _errorClassifier;
        private readonly ConcurrentCreationRace _race;

        public ConcurrentCreationUnitOfWork(
            EcommerceTxPrDbContext context,
            IDatabaseErrorClassifier errorClassifier,
            ConcurrentCreationRace race)
        {
            _context = context;
            _errorClassifier = errorClassifier;
            _race = race;
        }

        public async Task<SaveChangesResult> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            var addedPendingPayment = _context.ChangeTracker
                .Entries<Payment>()
                .SingleOrDefault(entry => entry.State == EntityState.Added
                    && entry.Entity.Status == PaymentStatus.Pending)
                ?.Entity;

            if (!_race.IsEnabled || addedPendingPayment is null)
            {
                return await SaveCoreAsync(cancellationToken);
            }

            var arrival = _race.Register(addedPendingPayment.Id);

            if (arrival == 1)
            {
                await _race.WaitForWinnerAsync(cancellationToken);
                return await SaveCoreAsync(cancellationToken);
            }

            if (arrival != 2)
            {
                throw new InvalidOperationException(
                    "Only two payment candidates were expected.");
            }

            await _race.WaitForFirstCandidateAsync(cancellationToken);

            try
            {
                var result = await SaveCoreAsync(cancellationToken);

                if (result != SaveChangesResult.Success)
                {
                    throw new InvalidOperationException(
                        $"The selected winner failed to persist: {result}.");
                }

                _race.CompleteWinner(addedPendingPayment.Id);
                return result;
            }
            catch (Exception exception)
            {
                _race.FailWinner(exception);
                throw;
            }
        }

        private Task<SaveChangesResult> SaveCoreAsync(
            CancellationToken cancellationToken)
        {
            return new EfUnitOfWork(_context, _errorClassifier)
                .SaveChangesAsync(cancellationToken);
        }
    }
}
