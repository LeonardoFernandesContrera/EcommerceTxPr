using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentConflictPersistenceTests
{
    [Fact]
    public async Task Duplicate_order_payment_is_classified_and_losing_graph_is_abandoned()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var orderId = await SeedPlacedOrderAsync(factory);

        using (var winningScope = factory.Services.CreateScope())
        {
            var winningContext = winningScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var winningPayment = new Payment(orderId, 10m);
            winningPayment.MarkFailed("Declined");
            winningContext.Payments.Add(winningPayment);
            await winningContext.SaveChangesAsync();
        }

        using var losingScope = factory.Services.CreateScope();
        var losingContext = losingScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var losingPayment = new Payment(orderId, 10m);
        losingPayment.MarkSucceeded("losing-reference");
        losingContext.Payments.Add(losingPayment);
        var unitOfWork = losingScope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        var result = await unitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(SaveChangesResult.PaymentConflict, result);
        Assert.Empty(losingContext.ChangeTracker.Entries());
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(1, await verificationContext.Payments.CountAsync());
        Assert.False(await verificationContext.Payments.AnyAsync(
            payment => payment.Id == losingPayment.Id));
    }

    [Fact]
    public async Task Unrelated_unique_constraint_is_not_classified_as_payment_conflict()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        await ApiTestData.CreateProductAsync(
            client,
            "PAYMENT-UNRELATED-DUPLICATE-SKU");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        context.Products.Add(new Product(
            "PAYMENT-UNRELATED-DUPLICATE-SKU",
            "Another product",
            10m,
            1));
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<IUnitOfWork>();

        await Assert.ThrowsAsync<DbUpdateException>(
            () => unitOfWork.SaveChangesAsync(CancellationToken.None));

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static async Task<Guid> SeedPlacedOrderAsync(
        CustomerApiFactory factory)
    {
        var customer = new Customer(
            "Conflict Customer",
            new DateTime(1990, 1, 1));
        var product = new Product("PAYMENT-CONFLICT-SKU", "Product", 10m, 1);
        var order = new Order(customer.Id);
        order.AddItem(product.Id, product.Name, product.Price, 1);
        order.Place();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        context.Customers.Add(customer);
        context.Products.Add(product);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }
}
