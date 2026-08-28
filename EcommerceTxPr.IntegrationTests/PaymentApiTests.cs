using System.Net;
using System.Net.Http.Json;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentApiTests
{
    [Fact]
    public void Post_api_description_has_no_request_body()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var descriptions = factory.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups
            .Items
            .SelectMany(group => group.Items);
        var postPayment = Assert.Single(descriptions, description =>
            description.HttpMethod == HttpMethod.Post.Method
            && description.RelativePath
                == "api/orders/{orderId}/payments");

        Assert.DoesNotContain(
            postPayment.ParameterDescriptions,
            parameter => parameter.Source == BindingSource.Body);
        Assert.Contains(
            postPayment.SupportedResponseTypes,
            response => response.StatusCode == StatusCodes.Status200OK);
        Assert.Contains(
            postPayment.SupportedResponseTypes,
            response => response.StatusCode == StatusCodes.Status201Created);
        Assert.Contains(
            postPayment.SupportedResponseTypes,
            response => response.StatusCode
                == StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Post_success_creates_succeeded_payment_and_marks_order_paid()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            price: 12.50m,
            stockQuantity: 5);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id,
            quantity: 2);

        using var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(25m, payment.Amount);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal($"test-provider-reference", payment.ProviderReference);
        Assert.Null(payment.FailureCode);
        Assert.NotNull(response.Headers.Location);

        using var orderResponse = await client.GetAsync($"/api/orders/{order.Id}");
        var paidOrder = await orderResponse.Content
            .ReadFromJsonAsync<EcommerceTxPr.Application.Orders.Contracts.OrderResponse>();
        Assert.NotNull(paidOrder);
        Assert.Equal(OrderStatus.Paid, paidOrder.Status);

        using var getResponse = await client.GetAsync(
            $"/api/orders/{order.Id}/payment");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var returned = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(returned);
        Assert.Equal(payment, returned);
    }

    [Fact]
    public async Task Post_gateway_decline_creates_failed_payment_and_keeps_order_pending()
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
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(order.Total, payment.Amount);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("CardDeclined", payment.FailureCode);
        Assert.Null(payment.ProviderReference);
        using var orderResponse = await client.GetAsync($"/api/orders/{order.Id}");
        var pendingOrder = await orderResponse.Content
            .ReadFromJsonAsync<EcommerceTxPr.Application.Orders.Contracts.OrderResponse>();
        Assert.NotNull(pendingOrder);
        Assert.Equal(OrderStatus.Pending, pendingOrder.Status);
    }

    [Fact]
    public async Task Post_duplicate_success_replays_with_ok_without_second_gateway_call()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);

        using var firstResponse = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);
        using var duplicateResponse = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var created = await firstResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        var replayed = await duplicateResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(created);
        Assert.Equal(created, replayed);
        Assert.Single(gateway.Requests);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(1, await context.Payments.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Post_duplicate_failure_replays_with_ok_without_second_gateway_call()
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

        using var firstResponse = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);
        using var replayResponse = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var created = await firstResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        var replayed = await replayResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(created);
        Assert.NotNull(replayed);
        Assert.Equal(created, replayed);
        Assert.Equal(PaymentStatus.Failed, replayed.Status);
        Assert.Equal(1, gateway.GatewayRequestCount);
    }

    [Fact]
    public async Task Post_indeterminate_returns_503_and_preserves_pending_intent()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();
        gateway.Result = PaymentGatewayResult.Indeterminate();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        var order = await ApiTestData.CreateOrderAsync(
            client,
            customer.Id,
            product.Id);

        using var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments",
            content: null);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            "Payment.OutcomeIndeterminate");
        using var getResponse = await client.GetAsync(
            $"/api/orders/{order.Id}/payment");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var payment = await getResponse.Content
            .ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(OrderStatus.Pending, await context.Orders
            .Where(candidate => candidate.Id == order.Id)
            .Select(candidate => candidate.Status)
            .SingleAsync());
        Assert.Empty(await context.OutboxMessages.ToListAsync());
        Assert.Equal(1, gateway.GatewayRequestCount);
    }

    [Fact]
    public async Task Post_missing_order_returns_not_found_without_gateway_call()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var gateway = factory.Services
            .GetRequiredService<DeterministicTestPaymentGateway>();

        using var response = await client.PostAsync(
            $"/api/orders/{Guid.NewGuid()}/payments",
            content: null);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Order.NotFound");
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Get_missing_payment_returns_not_found()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync(
            $"/api/orders/{Guid.NewGuid()}/payment");

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Payment.NotFound");
    }
}
