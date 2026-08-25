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

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentAtomicityTests
{
    [Fact]
    public async Task Losing_succeeded_payment_and_paid_order_roll_back_together()
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

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "Payment.AlreadyExists");
        Assert.True(race.HasRun);
        Assert.True(race.LosingTrackerWasCleared);
        Assert.Single(gateway.Requests);
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
    }

    private sealed class PaymentConflictRace
    {
        private int _hasRun;

        public bool IsEnabled { get; private set; }

        public bool HasRun => Volatile.Read(ref _hasRun) == 1;

        public bool LosingTrackerWasCleared { get; set; }

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
            if (_race.TryBegin())
            {
                await PersistWinningPaymentAsync(cancellationToken);
            }

            var result = await new EfUnitOfWork(_context, _errorClassifier)
                .SaveChangesAsync(cancellationToken);
            _race.LosingTrackerWasCleared =
                result == SaveChangesResult.PaymentConflict
                && !_context.ChangeTracker.Entries().Any();
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
            await winningContext.SaveChangesAsync(cancellationToken);
        }
    }
}
