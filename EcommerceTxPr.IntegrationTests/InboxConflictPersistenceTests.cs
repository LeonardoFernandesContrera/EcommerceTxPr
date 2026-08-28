using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class InboxConflictPersistenceTests
{
    [Fact]
    public async Task Inbox_primary_key_selects_one_complete_graph_and_classifies_loser()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var messageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var occurredOnUtc = new DateTime(
            2026,
            8,
            26,
            10,
            0,
            0,
            DateTimeKind.Utc);
        var processedOnUtc = occurredOnUtc.AddMinutes(1);
        using var winningScope = factory.Services.CreateScope();
        using var losingScope = factory.Services.CreateScope();
        var winningContext = winningScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var losingContext = losingScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        StageGraph(
            winningContext,
            messageId,
            occurredOnUtc,
            processedOnUtc,
            "winning-reference");
        StageGraph(
            losingContext,
            messageId,
            occurredOnUtc,
            processedOnUtc,
            "losing-reference");

        await winningContext.SaveChangesAsync();
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => losingContext.SaveChangesAsync());
        var classifier = losingScope.ServiceProvider
            .GetRequiredService<IDatabaseErrorClassifier>();

        Assert.True(classifier.IsInboxConflict(exception));
        losingContext.ChangeTracker.Clear();
        Assert.Empty(losingContext.ChangeTracker.Entries());
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(1, await verificationContext.InboxMessages.CountAsync());
        var projection = await verificationContext.PaymentEventProjections
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("winning-reference", projection.ProviderReference);
    }

    [Fact]
    public async Task Unrelated_unique_constraint_is_not_an_inbox_conflict()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        await ApiTestData.CreateProductAsync(client, "INBOX-UNRELATED-SKU");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        context.Products.Add(new Product(
            "INBOX-UNRELATED-SKU",
            "Another product",
            10m,
            1));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        var classifier = scope.ServiceProvider
            .GetRequiredService<IDatabaseErrorClassifier>();

        Assert.False(classifier.IsInboxConflict(exception));
    }

    private static void StageGraph(
        EcommerceTxPrDbContext context,
        Guid messageId,
        DateTime occurredOnUtc,
        DateTime processedOnUtc,
        string providerReference)
    {
        context.InboxMessages.Add(new InboxMessage(
            messageId,
            "payment.succeeded.v1",
            processedOnUtc));
        context.PaymentEventProjections.Add(
            PaymentEventProjection.Succeeded(
                messageId,
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                25m,
                providerReference,
                occurredOnUtc,
                processedOnUtc));
    }
}
