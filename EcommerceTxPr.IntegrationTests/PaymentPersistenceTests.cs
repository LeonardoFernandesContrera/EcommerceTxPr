using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentPersistenceTests
{
    [Fact]
    public async Task Payment_round_trips_through_explicit_repository()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var (orderId, total) = await SeedPlacedOrderAsync(factory);
        var payment = new Payment(orderId, total);
        payment.MarkFailed("Declined");

        using (var writeScope = factory.Services.CreateScope())
        {
            var repository = writeScope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();
            var unitOfWork = writeScope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            await repository.AddAsync(payment, CancellationToken.None);
            Assert.Equal(
                SaveChangesResult.Success,
                await unitOfWork.SaveChangesAsync(CancellationToken.None));
        }

        using var readScope = factory.Services.CreateScope();
        var readRepository = readScope.ServiceProvider
            .GetRequiredService<IPaymentRepository>();
        var byOrder = await readRepository.GetByOrderIdAsync(
            orderId,
            CancellationToken.None);
        var byId = await readRepository.GetByIdAsync(
            payment.Id,
            CancellationToken.None);

        Assert.NotNull(byOrder);
        Assert.NotNull(byId);
        Assert.Equal(payment.Id, byOrder.Id);
        Assert.Equal(orderId, byOrder.OrderId);
        Assert.Equal(total, byOrder.Amount);
        Assert.Equal(PaymentStatus.Failed, byOrder.Status);
        Assert.Equal("Declined", byOrder.FailureCode);
        Assert.Null(byOrder.ProviderReference);
        Assert.Equal(payment.Id, byId.Id);
        var context = readScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Order_read_is_no_tracking_while_payment_load_persists_mutation()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var (orderId, _) = await SeedPlacedOrderAsync(factory);

        using (var readScope = factory.Services.CreateScope())
        {
            var repository = readScope.ServiceProvider
                .GetRequiredService<IOrderRepository>();
            Assert.NotNull(await repository.GetByIdAsync(
                orderId,
                CancellationToken.None));
            var readContext = readScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            Assert.Empty(readContext.ChangeTracker.Entries());
        }

        using (var mutationScope = factory.Services.CreateScope())
        {
            var repository = mutationScope.ServiceProvider
                .GetRequiredService<IOrderRepository>();
            var order = await repository.GetByIdForPaymentAsync(
                orderId,
                CancellationToken.None);
            Assert.NotNull(order);
            order.MarkPaid();
            var unitOfWork = mutationScope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();
            Assert.Equal(
                SaveChangesResult.Success,
                await unitOfWork.SaveChangesAsync(CancellationToken.None));
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(
            OrderStatus.Paid,
            await verificationContext.Orders
                .AsNoTracking()
                .Where(order => order.Id == orderId)
                .Select(order => order.Status)
                .SingleAsync());
    }

    private static async Task<(Guid OrderId, decimal Total)> SeedPlacedOrderAsync(
        CustomerApiFactory factory)
    {
        var customer = new Customer(
            "Payment Customer",
            new DateTime(1990, 1, 1));
        var firstProduct = new Product("PAYMENT-SKU-1", "First", 12.50m, 5);
        var secondProduct = new Product("PAYMENT-SKU-2", "Second", 5m, 5);
        var order = new Order(customer.Id);
        order.AddItem(firstProduct.Id, firstProduct.Name, firstProduct.Price, 2);
        order.AddItem(secondProduct.Id, secondProduct.Name, secondProduct.Price, 3);
        order.Place();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        context.Customers.Add(customer);
        context.Products.AddRange(firstProduct, secondProduct);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return (order.Id, order.Total);
    }
}
