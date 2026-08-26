using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxAtLeastOnceTests
{
    [Fact]
    public async Task Confirmed_publish_with_failed_status_commit_republishes_same_message_id()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
            .UseSqlite(connection)
            .Options;
        var message = new OutboxMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}",
            new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc));

        await using (var seedContext = new EcommerceTxPrDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.OutboxMessages.Add(message);
            await seedContext.SaveChangesAsync();
        }

        var publisher = new DeterministicOutboxMessagePublisher();
        await using (var failingContext = new FailingSaveDbContext(options))
        {
            var dispatcher = CreateDispatcher(failingContext, publisher);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchBatchAsync(CancellationToken.None));
        }

        await using (var verificationContext = new EcommerceTxPrDbContext(options))
        {
            var pending = await verificationContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync();
            Assert.Null(pending.ProcessedOnUtc);
        }

        await using (var retryContext = new EcommerceTxPrDbContext(options))
        {
            var dispatcher = CreateDispatcher(retryContext, publisher);
            await dispatcher.DispatchBatchAsync(CancellationToken.None);
        }

        Assert.Equal(
            new[] { message.Id, message.Id },
            publisher.Requests.Select(request => request.Id));
    }

    private static OutboxDispatcher CreateDispatcher(
        EcommerceTxPrDbContext context,
        IOutboxMessagePublisher publisher)
    {
        return new OutboxDispatcher(
            context,
            publisher,
            Options.Create(new RabbitMqOptions { BatchSize = 20 }),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private sealed class FailingSaveDbContext
        : EcommerceTxPrDbContext
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
                "Test-only Outbox lifecycle persistence failure.");
        }
    }
}
