using System.Text;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentIntegrationEventAtomicityTests
{
    private const string Payload =
        "{\"paymentId\":\"22222222-2222-2222-2222-222222222222\","
        + "\"orderId\":\"33333333-3333-3333-3333-333333333333\","
        + "\"amount\":25.50,\"providerReference\":\"losing-reference\","
        + "\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}";

    [Fact]
    public async Task Ack_loss_redelivery_returns_duplicate_and_keeps_one_effect()
    {
        await using var database = await AtomicityDatabase.CreateAsync();
        var delivery = CreateDelivery(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        await using (var firstContext = database.CreateContext())
        {
            var first = await CreateProcessor(firstContext).ProcessAsync(
                delivery,
                CancellationToken.None);
            Assert.Equal(
                PaymentIntegrationEventProcessingResult.Processed,
                first);
        }

        await using (var redeliveryContext = database.CreateContext())
        {
            var redelivery = await CreateProcessor(redeliveryContext)
                .ProcessAsync(delivery, CancellationToken.None);
            Assert.Equal(
                PaymentIntegrationEventProcessingResult.Duplicate,
                redelivery);
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(1, await verificationContext.InboxMessages.CountAsync());
        Assert.Equal(
            1,
            await verificationContext.PaymentEventProjections.CountAsync());
    }

    [Fact]
    public async Task Failed_commit_persists_neither_record_and_later_retry_processes()
    {
        await using var database = await AtomicityDatabase.CreateAsync();
        var delivery = CreateDelivery(
            Guid.Parse("44444444-4444-4444-4444-444444444444"));

        await using (var failingContext = database.CreateFailingContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateProcessor(failingContext).ProcessAsync(
                    delivery,
                    CancellationToken.None));
        }

        await using (var verificationContext = database.CreateContext())
        {
            Assert.Equal(
                0,
                await verificationContext.InboxMessages.CountAsync());
            Assert.Equal(
                0,
                await verificationContext.PaymentEventProjections.CountAsync());
        }

        await using (var retryContext = database.CreateContext())
        {
            var retry = await CreateProcessor(retryContext).ProcessAsync(
                delivery,
                CancellationToken.None);
            Assert.Equal(
                PaymentIntegrationEventProcessingResult.Processed,
                retry);
        }

        await using var finalContext = database.CreateContext();
        Assert.Equal(1, await finalContext.InboxMessages.CountAsync());
        Assert.Equal(1, await finalContext.PaymentEventProjections.CountAsync());
    }

    [Fact]
    public async Task Inbox_race_loser_clears_graph_and_reconciles_as_duplicate()
    {
        await using var database = await AtomicityDatabase.CreateAsync();
        var messageId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var delivery = CreateDelivery(messageId);
        await using var losingContext = database.CreateRacingContext(
            cancellationToken => database.PersistWinnerAsync(
                messageId,
                cancellationToken));

        var result = await CreateProcessor(losingContext).ProcessAsync(
            delivery,
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Duplicate, result);
        Assert.Empty(losingContext.ChangeTracker.Entries());
        await using var verificationContext = database.CreateContext();
        Assert.Equal(1, await verificationContext.InboxMessages.CountAsync());
        var projection = await verificationContext.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("winning-reference", projection.ProviderReference);
    }

    [Fact]
    public async Task Inbox_race_with_different_winning_type_is_poison()
    {
        await using var database = await AtomicityDatabase.CreateAsync();
        var messageId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var delivery = CreateDelivery(messageId);
        await using var losingContext = database.CreateRacingContext(
            cancellationToken => database.PersistFailedWinnerAsync(
                messageId,
                cancellationToken));

        var result = await CreateProcessor(losingContext).ProcessAsync(
            delivery,
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Poison, result);
        Assert.Empty(losingContext.ChangeTracker.Entries());
        await using var verificationContext = database.CreateContext();
        Assert.Equal(1, await verificationContext.InboxMessages.CountAsync());
        var projection = await verificationContext.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(PaymentEventOutcome.Failed, projection.Outcome);
        Assert.Equal("WinningFailure", projection.FailureCode);
    }

    private static PaymentIntegrationEventProcessor CreateProcessor(
        EcommerceTxPrDbContext context)
    {
        return new PaymentIntegrationEventProcessor(
            context,
            new SqliteDatabaseErrorClassifier());
    }

    private static PaymentIntegrationEventDelivery CreateDelivery(
        Guid messageId)
    {
        return new PaymentIntegrationEventDelivery(
            messageId.ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            OutboxMessageTypes.PaymentSucceededV1,
            Encoding.UTF8.GetBytes(Payload));
    }

    private sealed class AtomicityDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EcommerceTxPrDbContext> _options;

        private AtomicityDatabase(SqliteConnection connection)
        {
            _connection = connection;
            _options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
                .UseSqlite(connection)
                .Options;
        }

        public static async Task<AtomicityDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new AtomicityDatabase(connection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public EcommerceTxPrDbContext CreateContext()
        {
            return new EcommerceTxPrDbContext(_options);
        }

        public FailingSaveDbContext CreateFailingContext()
        {
            return new FailingSaveDbContext(_options);
        }

        public RacingSaveDbContext CreateRacingContext(
            Func<CancellationToken, Task> beforeSave)
        {
            return new RacingSaveDbContext(_options, beforeSave);
        }

        public async Task PersistWinnerAsync(
            Guid messageId,
            CancellationToken cancellationToken)
        {
            var occurredOnUtc = new DateTime(
                2026,
                8,
                26,
                10,
                0,
                0,
                DateTimeKind.Utc);
            var processedOnUtc = occurredOnUtc.AddMinutes(1);
            await using var context = CreateContext();
            context.InboxMessages.Add(new InboxMessage(
                messageId,
                OutboxMessageTypes.PaymentSucceededV1,
                processedOnUtc));
            context.PaymentEventProjections.Add(
                PaymentEventProjection.Succeeded(
                    messageId,
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    25.50m,
                    "winning-reference",
                    occurredOnUtc,
                    processedOnUtc));
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task PersistFailedWinnerAsync(
            Guid messageId,
            CancellationToken cancellationToken)
        {
            var occurredOnUtc = new DateTime(
                2026,
                8,
                26,
                10,
                0,
                0,
                DateTimeKind.Utc);
            var processedOnUtc = occurredOnUtc.AddMinutes(1);
            await using var context = CreateContext();
            context.InboxMessages.Add(new InboxMessage(
                messageId,
                OutboxMessageTypes.PaymentFailedV1,
                processedOnUtc));
            context.PaymentEventProjections.Add(
                PaymentEventProjection.Failed(
                    messageId,
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    25.50m,
                    "WinningFailure",
                    occurredOnUtc,
                    processedOnUtc));
            await context.SaveChangesAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }
    }

    private sealed class FailingSaveDbContext : EcommerceTxPrDbContext
    {
        public FailingSaveDbContext(
            DbContextOptions<EcommerceTxPrDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Test-only failure before the Inbox transaction commits.");
        }
    }

    private sealed class RacingSaveDbContext : EcommerceTxPrDbContext
    {
        private readonly Func<CancellationToken, Task> _beforeSave;
        private int _hasRun;

        public RacingSaveDbContext(
            DbContextOptions<EcommerceTxPrDbContext> options,
            Func<CancellationToken, Task> beforeSave)
            : base(options)
        {
            _beforeSave = beforeSave;
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _hasRun, 1, 0) == 0)
            {
                await _beforeSave(cancellationToken);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
