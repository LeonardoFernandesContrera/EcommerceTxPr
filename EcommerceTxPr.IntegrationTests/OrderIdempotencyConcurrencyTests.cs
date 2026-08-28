using System.Net;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OrderIdempotencyConcurrencyTests
{
    [Fact]
    public async Task Product_concurrency_before_insert_reconciles_matching_winner_as_replay()
    {
        var race = new ProductConcurrencyRace();
        using var factory = CreateRaceFactory(race);
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 2);
        race.Enable(useDifferentRequestHash: false);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var response = await ApiTestData.PostOrderAsync(
            client,
            request,
            "concurrent-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(race.HasRun);
        await AssertWinningStateAsync(factory, product.Id, 1);
    }

    [Fact]
    public async Task Product_concurrency_before_insert_reconciles_different_winner_as_key_conflict()
    {
        var race = new ProductConcurrencyRace();
        using var factory = CreateRaceFactory(race);
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 2);
        race.Enable(useDifferentRequestHash: true);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var response = await ApiTestData.PostOrderAsync(
            client,
            request,
            "concurrent-key");

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "Order.IdempotencyKeyConflict");
        Assert.True(race.HasRun);
        await AssertWinningStateAsync(factory, product.Id, 1);
    }

    private static CustomerApiFactory CreateRaceFactory(
        ProductConcurrencyRace race)
    {
        return new CustomerApiFactory(services =>
        {
            services.AddSingleton(race);
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork, RacingUnitOfWork>();
        });
    }

    private static async Task AssertWinningStateAsync(
        CustomerApiFactory factory,
        Guid productId,
        int expectedStock)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(
            expectedStock,
            await context.Products
                .Where(product => product.Id == productId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.OrderIdempotencyRecords.CountAsync());
    }

    private sealed class ProductConcurrencyRace
    {
        private int _hasRun;

        public bool IsEnabled { get; private set; }

        public bool UseDifferentRequestHash { get; private set; }

        public bool HasRun => Volatile.Read(ref _hasRun) == 1;

        public void Enable(bool useDifferentRequestHash)
        {
            UseDifferentRequestHash = useDifferentRequestHash;
            IsEnabled = true;
        }

        public bool TryBegin()
        {
            return IsEnabled
                && Interlocked.CompareExchange(ref _hasRun, 1, 0) == 0;
        }
    }

    private sealed class RacingUnitOfWork : IUnitOfWork
    {
        private readonly EcommerceTxPrDbContext _context;
        private readonly IDatabaseErrorClassifier _errorClassifier;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ProductConcurrencyRace _race;

        public RacingUnitOfWork(
            EcommerceTxPrDbContext context,
            IDatabaseErrorClassifier errorClassifier,
            IServiceScopeFactory scopeFactory,
            ProductConcurrencyRace race)
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
                await PersistWinnerAsync(cancellationToken);
            }

            return await new EfUnitOfWork(_context, _errorClassifier)
                .SaveChangesAsync(cancellationToken);
        }

        private async Task PersistWinnerAsync(
            CancellationToken cancellationToken)
        {
            var losingOrder = _context.ChangeTracker
                .Entries<Order>()
                .Single(entry => entry.State == EntityState.Added)
                .Entity;
            var losingRecord = _context.ChangeTracker
                .Entries<OrderIdempotencyRecord>()
                .Single(entry => entry.State == EntityState.Added)
                .Entity;
            var productIds = losingOrder.Items
                .Select(item => item.ProductId)
                .ToArray();

            using var scope = _scopeFactory.CreateScope();
            var winningContext = scope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var winningProducts = await winningContext.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);
            var winningOrder = new Order(losingOrder.CustomerId);

            foreach (var item in losingOrder.Items)
            {
                var product = winningProducts[item.ProductId];
                winningOrder.AddItem(
                    product.Id,
                    product.Name,
                    product.Price,
                    item.Quantity);
                product.DecreaseStock(item.Quantity);
            }

            winningOrder.Place();
            var requestHash = _race.UseDifferentRequestHash
                ? new string('F', 64)
                : losingRecord.RequestHash;
            winningContext.Orders.Add(winningOrder);
            winningContext.OrderIdempotencyRecords.Add(
                new OrderIdempotencyRecord(
                    losingRecord.KeyHash,
                    requestHash,
                    winningOrder.Id));
            await winningContext.SaveChangesAsync(cancellationToken);
        }
    }
}
