using System.Text;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentIntegrationEventProcessorTests
{
    private const string SucceededPayload =
        "{\"paymentId\":\"22222222-2222-2222-2222-222222222222\","
        + "\"orderId\":\"33333333-3333-3333-3333-333333333333\","
        + "\"amount\":25.50,\"providerReference\":\"provider-123\","
        + "\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}";

    private const string FailedPayload =
        "{\"paymentId\":\"44444444-4444-4444-4444-444444444444\","
        + "\"orderId\":\"55555555-5555-5555-5555-555555555555\","
        + "\"amount\":10.00,\"failureCode\":\"CardDeclined\","
        + "\"occurredOnUtc\":\"2026-08-26T10:01:00Z\"}";

    [Fact]
    public async Task New_succeeded_message_persists_one_atomic_audit_effect()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        await using var context = database.CreateCountingContext();
        var messageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var processor = CreateProcessor(context);

        var result = await processor.ProcessAsync(
            Delivery(
                messageId.ToString("D"),
                OutboxMessageTypes.PaymentSucceededV1,
                SucceededPayload),
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Processed, result);
        Assert.Equal(1, context.SaveChangesCalls);
        var inbox = await context.InboxMessages.AsNoTracking().SingleAsync();
        var projection = await context.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(messageId, inbox.MessageId);
        Assert.Equal(OutboxMessageTypes.PaymentSucceededV1, inbox.Type);
        Assert.Equal(messageId, projection.MessageId);
        Assert.Equal(PaymentEventOutcome.Succeeded, projection.Outcome);
        Assert.Equal(Guid.Parse(
            "22222222-2222-2222-2222-222222222222"), projection.PaymentId);
        Assert.Equal(Guid.Parse(
            "33333333-3333-3333-3333-333333333333"), projection.OrderId);
        Assert.Equal(25.50m, projection.Amount);
        Assert.Equal("provider-123", projection.ProviderReference);
        Assert.Null(projection.FailureCode);
        Assert.Equal(
            new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc),
            projection.OccurredOnUtc);
        Assert.Equal(inbox.ProcessedOnUtc, projection.ProcessedOnUtc);
    }

    [Fact]
    public async Task New_failed_message_persists_failed_projection()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        await using var context = database.CreateCountingContext();
        var processor = CreateProcessor(context);

        var result = await processor.ProcessAsync(
            Delivery(
                "66666666-6666-6666-6666-666666666666",
                OutboxMessageTypes.PaymentFailedV1,
                FailedPayload),
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Processed, result);
        Assert.Equal(1, context.SaveChangesCalls);
        var projection = await context.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(PaymentEventOutcome.Failed, projection.Outcome);
        Assert.Null(projection.ProviderReference);
        Assert.Equal("CardDeclined", projection.FailureCode);
    }

    [Fact]
    public async Task Existing_inbox_identity_returns_duplicate_without_payload_read_or_save()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        var messageId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        await database.SeedSucceededAsync(messageId, "winning-reference");
        await using var context = database.CreateCountingContext();
        var processor = CreateProcessor(context);

        var result = await processor.ProcessAsync(
            Delivery(
                messageId.ToString("D"),
                OutboxMessageTypes.PaymentSucceededV1,
                "not-json"),
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Duplicate, result);
        Assert.Equal(0, context.SaveChangesCalls);
        Assert.Equal(1, await context.PaymentEventProjections.CountAsync());
        Assert.Equal(
            "winning-reference",
            await context.PaymentEventProjections
                .Select(projection => projection.ProviderReference)
                .SingleAsync());
    }

    [Fact]
    public async Task Existing_identity_with_different_type_is_poison_without_save()
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        var messageId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        await database.SeedSucceededAsync(messageId, "winning-reference");
        await using var context = database.CreateCountingContext();
        var processor = CreateProcessor(context);

        var result = await processor.ProcessAsync(
            Delivery(
                messageId.ToString("D"),
                OutboxMessageTypes.PaymentFailedV1,
                "not-json"),
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Poison, result);
        Assert.Equal(0, context.SaveChangesCalls);
        Assert.Equal(1, await context.InboxMessages.CountAsync());
        var projection = await context.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(PaymentEventOutcome.Succeeded, projection.Outcome);
        Assert.Equal("winning-reference", projection.ProviderReference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Missing_or_invalid_message_id_is_poison(string? messageId)
    {
        await AssertPoisonAsync(Delivery(
            messageId,
            OutboxMessageTypes.PaymentSucceededV1,
            SucceededPayload));
    }

    [Fact]
    public async Task Unknown_type_is_poison()
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            "payment.unknown.v1",
            SucceededPayload));
    }

    [Fact]
    public async Task Type_and_routing_key_mismatch_is_poison()
    {
        await AssertPoisonAsync(new PaymentIntegrationEventDelivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            OutboxMessageTypes.PaymentFailedV1,
            Encoding.UTF8.GetBytes(SucceededPayload)));
    }

    [Fact]
    public async Task Malformed_json_is_poison()
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            "{not-json"));
    }

    [Fact]
    public async Task Missing_body_is_poison()
    {
        await AssertPoisonAsync(new PaymentIntegrationEventDelivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            OutboxMessageTypes.PaymentSucceededV1,
            null!));
    }

    [Fact]
    public async Task Oversized_provider_reference_is_poison()
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            SucceededPayload.Replace(
                "provider-123",
                new string('a', 201),
                StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("{\"paymentId\":\"00000000-0000-0000-0000-000000000000\",\"orderId\":\"33333333-3333-3333-3333-333333333333\",\"amount\":25.50,\"providerReference\":\"provider\",\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}")]
    [InlineData("{\"paymentId\":\"22222222-2222-2222-2222-222222222222\",\"orderId\":\"00000000-0000-0000-0000-000000000000\",\"amount\":25.50,\"providerReference\":\"provider\",\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}")]
    [InlineData("{\"paymentId\":\"22222222-2222-2222-2222-222222222222\",\"orderId\":\"33333333-3333-3333-3333-333333333333\",\"amount\":0,\"providerReference\":\"provider\",\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}")]
    [InlineData("{\"paymentId\":\"22222222-2222-2222-2222-222222222222\",\"orderId\":\"33333333-3333-3333-3333-333333333333\",\"amount\":25.50,\"providerReference\":\"   \",\"occurredOnUtc\":\"2026-08-26T10:00:00Z\"}")]
    [InlineData("{\"paymentId\":\"22222222-2222-2222-2222-222222222222\",\"orderId\":\"33333333-3333-3333-3333-333333333333\",\"amount\":25.50,\"providerReference\":\"provider\",\"occurredOnUtc\":\"2026-08-26T10:00:00\"}")]
    public async Task Invalid_succeeded_payload_is_poison(string payload)
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentSucceededV1,
            payload));
    }

    [Fact]
    public async Task Failed_payload_without_failure_code_is_poison()
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentFailedV1,
            "{\"paymentId\":\"44444444-4444-4444-4444-444444444444\","
            + "\"orderId\":\"55555555-5555-5555-5555-555555555555\","
            + "\"amount\":10.00,\"failureCode\":\"\","
            + "\"occurredOnUtc\":\"2026-08-26T10:01:00Z\"}"));
    }

    [Fact]
    public async Task Oversized_failure_code_is_poison()
    {
        await AssertPoisonAsync(Delivery(
            Guid.NewGuid().ToString("D"),
            OutboxMessageTypes.PaymentFailedV1,
            FailedPayload.Replace(
                "CardDeclined",
                new string('a', 101),
                StringComparison.Ordinal)));
    }

    private static async Task AssertPoisonAsync(
        PaymentIntegrationEventDelivery delivery)
    {
        await using var database = await ProcessorDatabase.CreateAsync();
        await using var context = database.CreateCountingContext();
        var processor = CreateProcessor(context);

        var result = await processor.ProcessAsync(
            delivery,
            CancellationToken.None);

        Assert.Equal(PaymentIntegrationEventProcessingResult.Poison, result);
        Assert.Equal(0, context.SaveChangesCalls);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await context.InboxMessages.CountAsync());
        Assert.Equal(0, await context.PaymentEventProjections.CountAsync());
    }

    private static PaymentIntegrationEventProcessor CreateProcessor(
        EcommerceTxPrDbContext context)
    {
        return new PaymentIntegrationEventProcessor(
            context,
            new SqliteDatabaseErrorClassifier());
    }

    private static PaymentIntegrationEventDelivery Delivery(
        string? messageId,
        string type,
        string payload)
    {
        return new PaymentIntegrationEventDelivery(
            messageId,
            type,
            type,
            Encoding.UTF8.GetBytes(payload));
    }

    private sealed class ProcessorDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<EcommerceTxPrDbContext> _options;

        private ProcessorDatabase(SqliteConnection connection)
        {
            _connection = connection;
            _options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
                .UseSqlite(connection)
                .Options;
        }

        public static async Task<ProcessorDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new ProcessorDatabase(connection);
            await using var context = new EcommerceTxPrDbContext(database._options);
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public CountingDbContext CreateCountingContext()
        {
            return new CountingDbContext(_options);
        }

        public async Task SeedSucceededAsync(
            Guid messageId,
            string providerReference)
        {
            var occurredOnUtc = new DateTime(
                2026,
                8,
                26,
                9,
                0,
                0,
                DateTimeKind.Utc);
            var processedOnUtc = occurredOnUtc.AddMinutes(1);
            await using var context = new EcommerceTxPrDbContext(_options);
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
                    providerReference,
                    occurredOnUtc,
                    processedOnUtc));
            await context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }
    }

    private sealed class CountingDbContext : EcommerceTxPrDbContext
    {
        public CountingDbContext(
            DbContextOptions<EcommerceTxPrDbContext> options)
            : base(options)
        {
        }

        public int SaveChangesCalls { get; private set; }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
