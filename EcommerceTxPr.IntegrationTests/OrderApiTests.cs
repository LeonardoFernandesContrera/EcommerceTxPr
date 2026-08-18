using System.Net;
using System.Net.Http.Json;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.IntegrationTests.Infrastructure;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OrderApiTests
{
    [Fact]
    public async Task Post_then_get_returns_customer_items_and_calculated_totals()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var firstProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-001",
            "First Product",
            100m);
        var secondProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-002",
            "Second Product",
            25m);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(firstProduct.Id, 2),
                new CreateOrderItemRequest(secondProduct.Id, 3)
            });

        using var postResponse = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(customer.Id, created.CustomerId);
        Assert.Equal(OrderStatus.Pending, created.Status);
        Assert.Equal(275m, created.Total);

        var location = postResponse.Headers.Location;
        Assert.NotNull(location);
        var locationPath = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString;
        Assert.Equal($"/api/orders/{created.Id}", locationPath);

        using var getResponse = await client.GetAsync(locationPath);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var returned = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(returned);
        Assert.Equal(created.Id, returned.Id);
        Assert.Equal(customer.Id, returned.CustomerId);
        Assert.Equal(OrderStatus.Pending, returned.Status);
        Assert.Equal(275m, returned.Total);
        Assert.Collection(
            returned.Items.OrderBy(item => item.ProductName),
            item => AssertItem(item, firstProduct, 2, 200m),
            item => AssertItem(item, secondProduct, 3, 75m));
    }

    [Fact]
    public async Task Post_with_unknown_customer_returns_not_found()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var product = await ApiTestData.CreateProductAsync(client);
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Customer.NotFound");
    }

    [Fact]
    public async Task Post_with_unknown_product_returns_not_found()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Product.NotFound");
    }

    [Fact]
    public async Task Post_with_inactive_product_returns_not_found()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(client);
        using var deleteResponse = await client.DeleteAsync(
            $"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Product.NotFound");
    }

    [Fact]
    public async Task Post_with_empty_items_returns_validation_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            Array.Empty<CreateOrderItemRequest>());

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        await ApiTestAssertions.AssertValidationProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Post_with_invalid_quantity_returns_validation_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 0) });

        using var response = await client.PostAsJsonAsync("/api/orders", request);

        await ApiTestAssertions.AssertValidationProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Get_unknown_order_returns_not_found_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Order.NotFound");
    }

    [Fact]
    public async Task Existing_order_keeps_historical_price_after_product_price_changes()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            price: 100m);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });
        using var postResponse = await client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var createdOrder = await postResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(createdOrder);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/products/{product.Id}",
            new UpdateProductRequest(product.Name, 150m));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var getResponse = await client.GetAsync(
            $"/api/orders/{createdOrder.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var historicalOrder = await getResponse.Content
            .ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(historicalOrder);
        var historicalItem = Assert.Single(historicalOrder.Items);
        Assert.Equal(100m, historicalItem.UnitPrice);
        Assert.Equal(200m, historicalItem.LineTotal);
        Assert.Equal(200m, historicalOrder.Total);
    }

    private static void AssertItem(
        OrderItemResponse item,
        ProductResponse product,
        int expectedQuantity,
        decimal expectedLineTotal)
    {
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(product.Name, item.ProductName);
        Assert.Equal(product.Price, item.UnitPrice);
        Assert.Equal(expectedQuantity, item.Quantity);
        Assert.Equal(expectedLineTotal, item.LineTotal);
    }
}
