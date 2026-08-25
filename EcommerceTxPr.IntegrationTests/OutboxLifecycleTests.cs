using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Events;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxLifecycleTests
{
    [Fact]
    public async Task Successful_save_commits_payment_order_and_outbox_once_then_clears_event()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new CountingDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var order = await SeedPlacedOrderAsync(context);
        context.ResetSaveChangesCalls();
        var payment = new Payment(order.Id, order.Total);
        payment.MarkSucceeded("provider-reference");
        order.MarkPaid();
        context.Payments.Add(payment);
        var unitOfWork = new EfUnitOfWork(
            context,
            new SqliteDatabaseErrorClassifier());

        var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(SaveChangesResult.Success, result);
        Assert.Equal(1, context.SaveChangesCalls);
        Assert.Empty(((IHasDomainEvents)payment).DomainEvents);
        Assert.Equal(1, await context.Payments.CountAsync());
        Assert.Equal(
            1,
            await context.Orders.CountAsync(candidate =>
                candidate.Status == EcommerceTxPr.Domain.Enums.OrderStatus.Paid));
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Later_unrelated_save_in_same_context_does_not_duplicate_event()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var order = await SeedPlacedOrderAsync(context);
        var payment = new Payment(order.Id, order.Total);
        payment.MarkFailed("Declined");
        context.Payments.Add(payment);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.Equal(
            SaveChangesResult.Success,
            await unitOfWork.SaveChangesAsync(CancellationToken.None));
        context.Customers.Add(new Customer(
            "Unrelated Customer",
            new DateTime(1991, 2, 2)));

        var secondResult = await unitOfWork.SaveChangesAsync(
            CancellationToken.None);

        Assert.Equal(SaveChangesResult.Success, secondResult);
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Unknown_event_aborts_before_save_and_remains_pending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new CountingDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var aggregate = new UnknownEventAggregate();
        context.Set<UnknownEventAggregate>().Add(aggregate);
        var unitOfWork = new EfUnitOfWork(
            context,
            new SqliteDatabaseErrorClassifier());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.SaveChangesAsync(CancellationToken.None));

        Assert.Contains(nameof(UnknownDomainEvent), exception.Message);
        Assert.Equal(0, context.SaveChangesCalls);
        Assert.Empty(context.OutboxMessages.Local);
        Assert.Equal(2, ((IHasDomainEvents)aggregate).DomainEvents.Count);
        Assert.Equal(EntityState.Added, context.Entry(aggregate).State);
    }

    private static async Task<Order> SeedPlacedOrderAsync(
        EcommerceTxPrDbContext context)
    {
        var customer = new Customer(
            "Outbox Customer",
            new DateTime(1990, 1, 1));
        var product = new Product("OUTBOX-LIFECYCLE-SKU", "Product", 10m, 1);
        var order = new Order(customer.Id);
        order.AddItem(product.Id, product.Name, product.Price, 1);
        order.Place();
        context.Customers.Add(customer);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private sealed class CountingDbContext
        : EcommerceTxPrDbContext
    {
        public CountingDbContext(
            DbContextOptions<EcommerceTxPrDbContext> options)
            : base(options)
        {
        }

        public int SaveChangesCalls { get; private set; }

        public void ResetSaveChangesCalls()
        {
            SaveChangesCalls = 0;
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UnknownEventAggregate>(builder =>
            {
                builder.HasKey(aggregate => aggregate.Id);
                builder.Property(aggregate => aggregate.Id)
                    .ValueGeneratedNever();
            });
        }
    }

    private sealed class UnknownEventAggregate : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _domainEvents = new()
        {
            new PaymentFailedDomainEvent(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                10m,
                "KnownFailure",
                DateTime.UtcNow),
            new UnknownDomainEvent(DateTime.UtcNow)
        };

        public Guid Id { get; } = Guid.NewGuid();

        IReadOnlyCollection<IDomainEvent> IHasDomainEvents.DomainEvents =>
            _domainEvents.AsReadOnly();

        void IHasDomainEvents.ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }

    private sealed record UnknownDomainEvent(DateTime OccurredOnUtc)
        : IDomainEvent;
}
