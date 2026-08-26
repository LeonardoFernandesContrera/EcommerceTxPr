using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task Pending_message_is_published_exactly_and_marked_processed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var message = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}",
            hour: 10);
        await database.SeedAsync(message);
        var publisher = new DeterministicOutboxMessagePublisher();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(1, 1, 0), result);
        var request = Assert.Single(publisher.Requests);
        Assert.Equal(message.Id, request.Id);
        Assert.Equal(message.Type, request.Type);
        Assert.Equal(message.Payload, request.Payload);
        Assert.Equal(
            DateTimeKind.Utc,
            Assert.Single(context.OutboxMessages.Local)
                .ProcessedOnUtc!
                .Value
                .Kind);
        context.ChangeTracker.Clear();
        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.NotNull(persisted.ProcessedOnUtc);
        Assert.Null(persisted.Error);
    }

    [Fact]
    public async Task Publication_failure_records_safe_category_and_keeps_message_pending()
    {
        await using var database = await TestDatabase.CreateAsync();
        var message = CreateMessage(
            OutboxMessageTypes.PaymentFailedV1,
            "{\"failureCode\":\"Declined\"}",
            hour: 10);
        await database.SeedAsync(message);
        var publisher = new DeterministicOutboxMessagePublisher();
        publisher.EnqueueFailure(OutboxPublicationFailureCategory.Connection);
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(1, 0, 1), result);
        context.ChangeTracker.Clear();
        var persisted = await context.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Null(persisted.ProcessedOnUtc);
        Assert.Equal(
            "RabbitMQ publication failed (Connection).",
            persisted.Error);
    }

    [Fact]
    public async Task Mixed_batch_stops_after_failure_and_does_not_mutate_later_message()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"sequence\":1}",
            hour: 10);
        var second = CreateMessage(
            OutboxMessageTypes.PaymentFailedV1,
            "{\"sequence\":2}",
            hour: 11);
        var third = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"sequence\":3}",
            hour: 12);
        await database.SeedAsync(third, first, second);
        var publisher = new DeterministicOutboxMessagePublisher();
        publisher.EnqueueSuccess();
        publisher.EnqueueFailure(
            OutboxPublicationFailureCategory.ConfirmationOrRouting);
        publisher.EnqueueSuccess();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(3, 1, 1), result);
        Assert.Equal(
            new[] { first.Id, second.Id },
            publisher.Requests.Select(request => request.Id));
        context.ChangeTracker.Clear();
        var persisted = await context.OutboxMessages
            .AsNoTracking()
            .ToDictionaryAsync(message => message.Id);
        Assert.NotNull(persisted[first.Id].ProcessedOnUtc);
        Assert.Null(persisted[second.Id].ProcessedOnUtc);
        Assert.Equal(
            "RabbitMQ publication failed (ConfirmationOrRouting).",
            persisted[second.Id].Error);
        Assert.Null(persisted[third.Id].ProcessedOnUtc);
        Assert.Null(persisted[third.Id].Error);
    }

    [Fact]
    public async Task Query_orders_deterministically_and_respects_batch_size()
    {
        await using var database = await TestDatabase.CreateAsync();
        var later = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"sequence\":3}",
            hour: 12);
        var first = CreateMessage(
            OutboxMessageTypes.PaymentFailedV1,
            "{\"sequence\":1}",
            hour: 10);
        var second = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"sequence\":2}",
            hour: 11);
        await database.SeedAsync(later, second, first);
        var publisher = new DeterministicOutboxMessagePublisher();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher, batchSize: 2);

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(2, 2, 0), result);
        Assert.Equal(
            new[] { first.Id, second.Id },
            publisher.Requests.Select(request => request.Id));
    }

    [Fact]
    public async Task Processed_message_is_not_published_again()
    {
        await using var database = await TestDatabase.CreateAsync();
        var message = CreateMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"value\":1}",
            hour: 10);
        message.MarkProcessed(
            new DateTime(2026, 8, 25, 11, 0, 0, DateTimeKind.Utc));
        await database.SeedAsync(message);
        var publisher = new DeterministicOutboxMessagePublisher();
        await using var context = database.CreateContext();
        var dispatcher = CreateDispatcher(context, publisher);

        var result = await dispatcher.DispatchBatchAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(0, 0, 0), result);
        Assert.Empty(publisher.Requests);
    }

    [Fact]
    public async Task Failed_message_retries_with_same_stable_id()
    {
        await using var database = await TestDatabase.CreateAsync();
        var message = CreateMessage(
            OutboxMessageTypes.PaymentFailedV1,
            "{\"value\":1}",
            hour: 10);
        await database.SeedAsync(message);
        var publisher = new DeterministicOutboxMessagePublisher();
        publisher.EnqueueFailure(OutboxPublicationFailureCategory.Publish);
        publisher.EnqueueSuccess();

        await using (var firstContext = database.CreateContext())
        {
            var firstDispatcher = CreateDispatcher(firstContext, publisher);
            await firstDispatcher.DispatchBatchAsync(CancellationToken.None);
        }

        await using (var secondContext = database.CreateContext())
        {
            var secondDispatcher = CreateDispatcher(secondContext, publisher);
            await secondDispatcher.DispatchBatchAsync(CancellationToken.None);
        }

        Assert.Equal(
            new[] { message.Id, message.Id },
            publisher.Requests.Select(request => request.Id));
    }

    private static OutboxDispatcher CreateDispatcher(
        EcommerceTxPrDbContext context,
        IOutboxMessagePublisher publisher,
        int batchSize = 20)
    {
        return new OutboxDispatcher(
            context,
            publisher,
            Options.Create(new RabbitMqOptions { BatchSize = batchSize }),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private static OutboxMessage CreateMessage(
        string type,
        string payload,
        int hour)
    {
        return new OutboxMessage(
            type,
            payload,
            new DateTime(2026, 8, 25, hour, 0, 0, DateTimeKind.Utc));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EcommerceTxPrDbContext> _options;

        private TestDatabase(SqliteConnection connection)
        {
            _connection = connection;
            _options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
                .UseSqlite(connection)
                .Options;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new TestDatabase(connection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public EcommerceTxPrDbContext CreateContext()
        {
            return new EcommerceTxPrDbContext(_options);
        }

        public async Task SeedAsync(params OutboxMessage[] messages)
        {
            await using var context = CreateContext();
            context.OutboxMessages.AddRange(messages);
            await context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }
    }
}
