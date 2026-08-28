using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class InboxPersistenceTests
{
    [Fact]
    public async Task Succeeded_and_failed_projections_round_trip_with_inbox_identity()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var succeededMessageId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");
        var failedMessageId = Guid.Parse(
            "22222222-2222-2222-2222-222222222222");
        var succeededOccurredOnUtc = new DateTime(
            2026,
            8,
            26,
            10,
            0,
            0,
            DateTimeKind.Utc);
        var failedOccurredOnUtc = succeededOccurredOnUtc.AddMinutes(1);
        var processedOnUtc = succeededOccurredOnUtc.AddMinutes(2);

        using (var writeScope = factory.Services.CreateScope())
        {
            var context = writeScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            context.InboxMessages.AddRange(
                new InboxMessage(
                    succeededMessageId,
                    "payment.succeeded.v1",
                    processedOnUtc),
                new InboxMessage(
                    failedMessageId,
                    "payment.failed.v1",
                    processedOnUtc));
            context.PaymentEventProjections.AddRange(
                PaymentEventProjection.Succeeded(
                    succeededMessageId,
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    25.50m,
                    "provider-123",
                    succeededOccurredOnUtc,
                    processedOnUtc),
                PaymentEventProjection.Failed(
                    failedMessageId,
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    10m,
                    "CardDeclined",
                    failedOccurredOnUtc,
                    processedOnUtc));

            Assert.Equal(4, await context.SaveChangesAsync());
        }

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var inbox = await readContext.InboxMessages
            .AsNoTracking()
            .OrderBy(message => message.MessageId)
            .ToArrayAsync();
        var projections = await readContext.PaymentEventProjections
            .AsNoTracking()
            .OrderBy(projection => projection.MessageId)
            .ToArrayAsync();

        Assert.Collection(
            inbox,
            message =>
            {
                Assert.Equal(succeededMessageId, message.MessageId);
                Assert.Equal("payment.succeeded.v1", message.Type);
                Assert.Equal(processedOnUtc, message.ProcessedOnUtc);
            },
            message =>
            {
                Assert.Equal(failedMessageId, message.MessageId);
                Assert.Equal("payment.failed.v1", message.Type);
                Assert.Equal(processedOnUtc, message.ProcessedOnUtc);
            });
        Assert.Collection(
            projections,
            projection =>
            {
                Assert.Equal(succeededMessageId, projection.MessageId);
                Assert.Equal(PaymentEventOutcome.Succeeded, projection.Outcome);
                Assert.Equal(25.50m, projection.Amount);
                Assert.Equal("provider-123", projection.ProviderReference);
                Assert.Null(projection.FailureCode);
                Assert.Equal(succeededOccurredOnUtc, projection.OccurredOnUtc);
                Assert.Equal(processedOnUtc, projection.ProcessedOnUtc);
            },
            projection =>
            {
                Assert.Equal(failedMessageId, projection.MessageId);
                Assert.Equal(PaymentEventOutcome.Failed, projection.Outcome);
                Assert.Equal(10m, projection.Amount);
                Assert.Null(projection.ProviderReference);
                Assert.Equal("CardDeclined", projection.FailureCode);
                Assert.Equal(failedOccurredOnUtc, projection.OccurredOnUtc);
                Assert.Equal(processedOnUtc, projection.ProcessedOnUtc);
            });
        Assert.Empty(readContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Projection_requires_matching_inbox_message()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var timestamp = new DateTime(
            2026,
            8,
            26,
            12,
            0,
            0,
            DateTimeKind.Utc);
        context.PaymentEventProjections.Add(
            PaymentEventProjection.Succeeded(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                10m,
                "provider-reference",
                timestamp,
                timestamp));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }
}
