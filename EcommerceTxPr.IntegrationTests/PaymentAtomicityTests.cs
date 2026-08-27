using System.Net;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentAtomicityTests
{
    [Fact]
    public async Task Losing_terminal_concurrency_rolls_back_order_and_outbox_together()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var orderResponse = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);
        var payment = new Payment(orderResponse.Id, orderResponse.Total);

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            seedContext.Payments.Add(payment);
            await seedContext.SaveChangesAsync();
        }

        using var winningScope = factory.Services.CreateScope();
        using var losingScope = factory.Services.CreateScope();
        var winningContext = winningScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var losingContext = losingScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var winningPayment = await winningContext.Payments.SingleAsync(
            candidate => candidate.Id == payment.Id);
        var losingPayment = await losingContext.Payments.SingleAsync(
            candidate => candidate.Id == payment.Id);
        var losingOrder = await losingContext.Orders
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == orderResponse.Id);

        winningPayment.MarkFailed("WinningDecline");
        losingPayment.MarkSucceeded("losing-success");
        losingOrder.MarkPaid();
        var winningUnitOfWork = winningScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();
        var losingUnitOfWork = losingScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        Assert.Equal(
            SaveChangesResult.Success,
            await winningUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(
            SaveChangesResult.ConcurrencyConflict,
            await losingUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Empty(losingContext.ChangeTracker.Entries());

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persistedPayment = await context.Payments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == payment.Id);
        Assert.Equal(PaymentStatus.Failed, persistedPayment.Status);
        Assert.Equal("WinningDecline", persistedPayment.FailureCode);
        Assert.Equal(OrderStatus.Pending, await context.Orders
            .Where(order => order.Id == orderResponse.Id)
            .Select(order => order.Status)
            .SingleAsync());
        var outbox = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("payment.failed.v1", outbox.Type);
    }

    [Fact]
    public async Task Losing_pending_candidate_is_cleared_and_replays_winner()
    {
        var race = new PaymentConflictRace();
        using var factory = new CustomerApiFactory(services =>
        {
            services.AddSingleton(race);
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, PaymentConflictRaceUnitOfWork>();
        });
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        race.Enable();

        using var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(race.HasRun);
        Assert.True(race.LosingTrackerWasCleared);
        Assert.True(race.LosingPaymentStayedPendingWithoutDomainEvents);
        Assert.Empty(gateway.Requests);
        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(
            OrderStatus.Pending,
            await context.Orders
                .AsNoTracking()
                .Where(candidate => candidate.Id == order.Id)
                .Select(candidate => candidate.Status)
                .SingleAsync());
        var persistedPayment = await context.Payments
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrderId == order.Id);
        Assert.Equal(PaymentStatus.Failed, persistedPayment.Status);
        Assert.Equal("WinningPayment", persistedPayment.FailureCode);
        Assert.False(await context.Payments.AnyAsync(candidate =>
            candidate.Status == PaymentStatus.Succeeded));
        var persistedOutboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("payment.failed.v1", persistedOutboxMessage.Type);
        Assert.False(await context.OutboxMessages.AnyAsync(message =>
            message.Type == "payment.succeeded.v1"));
    }

    private sealed class PaymentConflictRace
    {
        private int _hasRun;

        public bool IsEnabled { get; private set; }

        public bool HasRun => Volatile.Read(ref _hasRun) == 1;

        public bool LosingTrackerWasCleared { get; set; }

        public bool LosingPaymentStayedPendingWithoutDomainEvents { get; set; }

        public void Enable()
        {
            IsEnabled = true;
        }

        public bool TryBegin()
        {
            return IsEnabled
                && Interlocked.CompareExchange(ref _hasRun, 1, 0) == 0;
        }
    }

    private sealed class PaymentConflictRaceUnitOfWork : IUnitOfWork
    {
        private readonly EcommerceTxPrDbContext _context;
        private readonly IDatabaseErrorClassifier _errorClassifier;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PaymentConflictRace _race;

        public PaymentConflictRaceUnitOfWork(
            EcommerceTxPrDbContext context,
            IDatabaseErrorClassifier errorClassifier,
            IServiceScopeFactory scopeFactory,
            PaymentConflictRace race)
        {
            _context = context;
            _errorClassifier = errorClassifier;
            _scopeFactory = scopeFactory;
            _race = race;
        }

        public async Task<SaveChangesResult> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            Payment? losingPayment = null;

            if (_race.TryBegin())
            {
                losingPayment = _context.ChangeTracker
                    .Entries<Payment>()
                    .Single(entry => entry.State == EntityState.Added)
                    .Entity;
                await PersistWinningPaymentAsync(cancellationToken);
            }

            var result = await new EfUnitOfWork(_context, _errorClassifier)
                .SaveChangesAsync(cancellationToken);
            _race.LosingTrackerWasCleared =
                result == SaveChangesResult.PaymentConflict
                && !_context.ChangeTracker.Entries().Any();
            _race.LosingPaymentStayedPendingWithoutDomainEvents =
                result == SaveChangesResult.PaymentConflict
                && losingPayment is not null
                && losingPayment.Status == PaymentStatus.Pending
                && ((IHasDomainEvents)losingPayment).DomainEvents.Count == 0;
            return result;
        }

        private async Task PersistWinningPaymentAsync(
            CancellationToken cancellationToken)
        {
            var losingPayment = _context.ChangeTracker
                .Entries<Payment>()
                .Single(entry => entry.State == EntityState.Added)
                .Entity;

            using var scope = _scopeFactory.CreateScope();
            var winningContext = scope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var winningPayment = new Payment(
                losingPayment.OrderId,
                losingPayment.Amount);
            winningPayment.MarkFailed("WinningPayment");
            winningContext.Payments.Add(winningPayment);
            var winningUnitOfWork = scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            Assert.Equal(
                SaveChangesResult.Success,
                await winningUnitOfWork.SaveChangesAsync(cancellationToken));
        }
    }
}
