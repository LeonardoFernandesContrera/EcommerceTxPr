using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentOutboxTests
{
    [Fact]
    public async Task Successful_payment_commits_paid_order_and_succeeded_v1_message()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            price: 12.50m,
            stockQuantity: 2);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id,
            quantity: 2);

        using var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var returnedPayment = await response.Content
            .ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(returnedPayment);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persistedPayment = await context.Payments
            .AsNoTracking()
            .SingleAsync(payment => payment.OrderId == order.Id);
        var persistedOrderStatus = await context.Orders
            .AsNoTracking()
            .Where(candidate => candidate.Id == order.Id)
            .Select(candidate => candidate.Status)
            .SingleAsync();
        var message = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(PaymentStatus.Succeeded, persistedPayment.Status);
        Assert.Equal(OrderStatus.Paid, persistedOrderStatus);
        Assert.Equal("payment.succeeded.v1", message.Type);
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        Assert.Equal(
            persistedPayment.Id,
            payload.GetProperty("paymentId").GetGuid());
        Assert.Equal(order.Id, payload.GetProperty("orderId").GetGuid());
        Assert.Equal(
            persistedPayment.Amount,
            payload.GetProperty("amount").GetDecimal());
        Assert.Equal(
            persistedPayment.ProviderReference,
            payload.GetProperty("providerReference").GetString());
        var occurredOnUtc = payload
            .GetProperty("occurredOnUtc")
            .GetDateTime();
        Assert.Equal(DateTimeKind.Utc, occurredOnUtc.Kind);
        Assert.Equal(message.OccurredOnUtc.Ticks, occurredOnUtc.Ticks);
    }

    [Fact]
    public async Task Failed_payment_commits_pending_order_and_failed_v1_message()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        gateway.Result = PaymentGatewayResult.Failed("CardDeclined");
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);

        using var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        var persistedPayment = await context.Payments
            .AsNoTracking()
            .SingleAsync(payment => payment.OrderId == order.Id);
        var persistedOrderStatus = await context.Orders
            .AsNoTracking()
            .Where(candidate => candidate.Id == order.Id)
            .Select(candidate => candidate.Status)
            .SingleAsync();
        var message = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(PaymentStatus.Failed, persistedPayment.Status);
        Assert.Equal(OrderStatus.Pending, persistedOrderStatus);
        Assert.Equal("payment.failed.v1", message.Type);
        using var document = JsonDocument.Parse(message.Payload);
        var payload = document.RootElement;
        Assert.Equal(
            persistedPayment.Id,
            payload.GetProperty("paymentId").GetGuid());
        Assert.Equal(order.Id, payload.GetProperty("orderId").GetGuid());
        Assert.Equal(
            persistedPayment.Amount,
            payload.GetProperty("amount").GetDecimal());
        Assert.Equal(
            "CardDeclined",
            payload.GetProperty("failureCode").GetString());
        var occurredOnUtc = payload
            .GetProperty("occurredOnUtc")
            .GetDateTime();
        Assert.Equal(DateTimeKind.Utc, occurredOnUtc.Kind);
        Assert.Equal(message.OccurredOnUtc.Ticks, occurredOnUtc.Ticks);
    }
}
