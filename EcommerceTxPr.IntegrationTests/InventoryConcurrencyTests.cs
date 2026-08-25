using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class InventoryConcurrencyTests
{
    [Fact]
    public async Task Stale_product_write_detects_conflict_and_cannot_oversell()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var productResponse = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 1);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var secondContext = secondScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var firstProduct = await firstContext.Products.SingleAsync(
            product => product.Id == productResponse.Id);
        var staleProduct = await secondContext.Products.SingleAsync(
            product => product.Id == productResponse.Id);
        var sharedVersion = firstProduct.Version;
        Assert.Equal(sharedVersion, staleProduct.Version);

        firstProduct.DecreaseStock(1);
        await firstContext.SaveChangesAsync();
        staleProduct.DecreaseStock(1);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persistedProduct = await verificationContext.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == productResponse.Id);
        Assert.Equal(0, persistedProduct.StockQuantity);
        Assert.NotEqual(sharedVersion, persistedProduct.Version);
    }

    [Fact]
    public async Task Stale_inventory_and_order_commit_rolls_back_the_failed_order()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var productResponse = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 2);
        using var staleScope = factory.Services.CreateScope();
        using var winningScope = factory.Services.CreateScope();
        var staleContext = staleScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var winningContext = winningScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var staleProduct = await staleContext.Products.SingleAsync(
            product => product.Id == productResponse.Id);
        var winningProduct = await winningContext.Products.SingleAsync(
            product => product.Id == productResponse.Id);
        var failedOrder = new Order(customer.Id);
        failedOrder.AddItem(
            staleProduct.Id,
            staleProduct.Name,
            staleProduct.Price,
            1);
        failedOrder.Place();
        staleProduct.DecreaseStock(1);
        staleContext.Orders.Add(failedOrder);

        winningProduct.DecreaseStock(1);
        await winningContext.SaveChangesAsync();

        var unitOfWork = staleScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();
        var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(SaveChangesResult.ConcurrencyConflict, result);
        Assert.Empty(staleContext.ChangeTracker.Entries());
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persistedProduct = await verificationContext.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == productResponse.Id);
        Assert.Equal(1, persistedProduct.StockQuantity);
        Assert.False(await verificationContext.Orders
            .AnyAsync(order => order.Id == failedOrder.Id));
    }
}
