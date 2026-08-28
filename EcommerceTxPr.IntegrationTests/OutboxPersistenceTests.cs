using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxPersistenceTests
{
    [Fact]
    public async Task Outbox_message_round_trips_with_future_lifecycle_unset()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var occurredOnUtc = new DateTime(
            2026,
            8,
            25,
            12,
            30,
            0,
            DateTimeKind.Utc);
        var message = new OutboxMessage(
            "payment.succeeded.v1",
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}",
            occurredOnUtc);

        using (var writeScope = factory.Services.CreateScope())
        {
            var context = writeScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
        }

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persisted = await readContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id);

        Assert.Equal(message.Id, persisted.Id);
        Assert.Equal("payment.succeeded.v1", persisted.Type);
        Assert.Equal(
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}",
            persisted.Payload);
        Assert.Equal(occurredOnUtc, persisted.OccurredOnUtc);
        Assert.Null(persisted.ProcessedOnUtc);
        Assert.Null(persisted.Error);
    }
}
