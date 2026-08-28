using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OrderIdempotencyPersistenceTests
{
    [Fact]
    public async Task Duplicate_key_is_classified_and_failed_graph_is_abandoned()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var keyHash = new string('A', 64);

        using (var winningScope = factory.Services.CreateScope())
        {
            var winningContext = winningScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var winningOrder = CreateOrder(
                customer.Id,
                product.Id,
                product.Name,
                product.Price);
            winningContext.Orders.Add(winningOrder);
            winningContext.OrderIdempotencyRecords.Add(
                new OrderIdempotencyRecord(
                    keyHash,
                    new string('B', 64),
                    winningOrder.Id));
            await winningContext.SaveChangesAsync();
        }

        using var losingScope = factory.Services.CreateScope();
        var losingContext = losingScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var losingOrder = CreateOrder(
            customer.Id,
            product.Id,
            product.Name,
            product.Price);
        losingContext.Orders.Add(losingOrder);
        losingContext.OrderIdempotencyRecords.Add(
            new OrderIdempotencyRecord(
                keyHash,
                new string('C', 64),
                losingOrder.Id));
        var unitOfWork = losingScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(SaveChangesResult.IdempotencyConflict, result);
        Assert.Empty(losingContext.ChangeTracker.Entries());
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(1, await verificationContext.Orders.CountAsync());
        Assert.Equal(
            1,
            await verificationContext.OrderIdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Unrelated_unique_constraint_is_not_classified_as_idempotency()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        await ApiTestData.CreateProductAsync(client, "DUPLICATE-SKU");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        context.Products.Add(new Product(
            "DUPLICATE-SKU",
            "Another product",
            10m,
            1));
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await Assert.ThrowsAsync<DbUpdateException>(
            () => unitOfWork.SaveChangesAsync(CancellationToken.None));
    }

    private static Order CreateOrder(
        Guid customerId,
        Guid productId,
        string productName,
        decimal unitPrice)
    {
        var order = new Order(customerId);
        order.AddItem(productId, productName, unitPrice, 1);
        order.Place();
        return order;
    }
}
