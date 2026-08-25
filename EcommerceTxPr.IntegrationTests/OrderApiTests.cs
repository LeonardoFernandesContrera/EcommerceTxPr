using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OrderApiTests
{
    [Fact]
    public void Post_api_description_declares_idempotency_header_and_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var descriptions = factory.Services
            .GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups
            .Items
            .SelectMany(group => group.Items);
        var postOrder = Assert.Single(descriptions.Where(description =>
            description.HttpMethod == HttpMethod.Post.Method
            && description.RelativePath == "api/orders"));

        var header = Assert.Single(postOrder.ParameterDescriptions.Where(
            parameter => parameter.Name == "Idempotency-Key"));
        Assert.Equal(BindingSource.Header, header.Source);
        var badRequest = Assert.Single(postOrder.SupportedResponseTypes.Where(
            response => response.StatusCode == StatusCodes.Status400BadRequest));
        Assert.Equal(typeof(ProblemDetails), badRequest.Type);
    }

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

        using var postResponse = await ApiTestData.PostOrderAsync(client, request);

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

    [Theory]
    [InlineData(null, "Order.IdempotencyKeyRequired")]
    [InlineData("", "Order.IdempotencyKeyRequired")]
    [InlineData("   ", "Order.IdempotencyKeyRequired")]
    public async Task Post_with_missing_or_blank_idempotency_key_returns_bad_request(
        string? idempotencyKey,
        string expectedCode)
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        using var response = await ApiTestData.PostOrderAsync(
            client,
            request,
            idempotencyKey);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            expectedCode);
        await AssertPersistenceCountsAsync(factory, 0, 0);
    }

    [Fact]
    public async Task Post_with_oversized_idempotency_key_returns_bad_request()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        using var response = await ApiTestData.PostOrderAsync(
            client,
            request,
            new string('a', 101));

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "Order.IdempotencyKeyTooLong");
        await AssertPersistenceCountsAsync(factory, 0, 0);
    }

    [Fact]
    public async Task Post_with_multiple_idempotency_keys_returns_bad_request()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        using var response = await ApiTestData.PostOrderAsync(
            client,
            request,
            headerValues: new[] { "first", "second" });

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "Order.IdempotencyKeyInvalid");
        await AssertPersistenceCountsAsync(factory, 0, 0);
    }

    [Fact]
    public async Task Post_same_key_and_request_returns_created_then_replayed_once()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 5);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        using var firstResponse = await ApiTestData.PostOrderAsync(
            client,
            request,
            "replay-key");
        using var secondResponse = await ApiTestData.PostOrderAsync(
            client,
            request,
            "replay-key");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var createdJson = await firstResponse.Content.ReadAsStringAsync();
        var replayedJson = await secondResponse.Content.ReadAsStringAsync();
        using var createdDocument = JsonDocument.Parse(createdJson);
        using var replayedDocument = JsonDocument.Parse(replayedJson);
        Assert.Equal(
            createdDocument.RootElement
                .GetProperty("creationDate")
                .GetString(),
            replayedDocument.RootElement
                .GetProperty("creationDate")
                .GetString());
        var created = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>();
        var replayed = await secondResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(created);
        Assert.NotNull(replayed);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Equal(created.CustomerId, replayed.CustomerId);
        Assert.Equal(created.Status, replayed.Status);
        Assert.Equal(created.CreationDate, replayed.CreationDate);
        Assert.Equal(created.Total, replayed.Total);
        Assert.Equal(created.Items, replayed.Items);
        using var productResponse = await client.GetAsync(
            $"/api/products/{product.Id}");
        var persistedProduct = await productResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(persistedProduct);
        Assert.Equal(3, persistedProduct.StockQuantity);
        await AssertPersistenceCountsAsync(factory, 1, 1);
    }

    [Fact]
    public async Task Post_same_key_and_different_request_returns_conflict_without_second_change()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 5);

        using var firstResponse = await ApiTestData.PostOrderAsync(
            client,
            new CreateOrderRequest(
                customer.Id,
                new[] { new CreateOrderItemRequest(product.Id, 1) }),
            "conflict-key");
        using var conflictResponse = await ApiTestData.PostOrderAsync(
            client,
            new CreateOrderRequest(
                customer.Id,
                new[] { new CreateOrderItemRequest(product.Id, 2) }),
            "conflict-key");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await ApiTestAssertions.AssertProblemDetailsAsync(
            conflictResponse,
            HttpStatusCode.Conflict,
            "Order.IdempotencyKeyConflict");
        using var productResponse = await client.GetAsync(
            $"/api/products/{product.Id}");
        var persistedProduct = await productResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(persistedProduct);
        Assert.Equal(4, persistedProduct.StockQuantity);
        await AssertPersistenceCountsAsync(factory, 1, 1);
    }

    [Fact]
    public async Task Post_same_key_replays_when_items_are_reordered_and_split()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var firstProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-ONE",
            stockQuantity: 10);
        var secondProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-TWO",
            stockQuantity: 10);
        var splitRequest = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(secondProduct.Id, 1),
                new CreateOrderItemRequest(firstProduct.Id, 2),
                new CreateOrderItemRequest(firstProduct.Id, 3)
            });
        var aggregatedRequest = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(firstProduct.Id, 5),
                new CreateOrderItemRequest(secondProduct.Id, 1)
            });

        using var firstResponse = await ApiTestData.PostOrderAsync(
            client,
            splitRequest,
            "normalized-key");
        using var replayResponse = await ApiTestData.PostOrderAsync(
            client,
            aggregatedRequest,
            "normalized-key");

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        var created = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>();
        var replayed = await replayResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(created);
        Assert.NotNull(replayed);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Equal(created.Items.ToArray(), replayed.Items.ToArray());
        await AssertPersistenceCountsAsync(factory, 1, 1);
    }

    [Fact]
    public async Task Post_keys_use_exact_case_sensitive_semantics_across_test_provider()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 2);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var upperResponse = await ApiTestData.PostOrderAsync(
            client,
            request,
            "CASE-KEY");
        using var lowerResponse = await ApiTestData.PostOrderAsync(
            client,
            request,
            "case-key");

        Assert.Equal(HttpStatusCode.Created, upperResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, lowerResponse.StatusCode);
        var upperOrder = await upperResponse.Content.ReadFromJsonAsync<OrderResponse>();
        var lowerOrder = await lowerResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(upperOrder);
        Assert.NotNull(lowerOrder);
        Assert.NotEqual(upperOrder.Id, lowerOrder.Id);
        await AssertPersistenceCountsAsync(factory, 2, 2);
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

        using var response = await ApiTestData.PostOrderAsync(client, request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound,
            "Customer.NotFound");
    }

    [Fact]
    public async Task Post_with_empty_customer_id_returns_validation_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var product = await ApiTestData.CreateProductAsync(client);
        var request = new CreateOrderRequest(
            Guid.Empty,
            new[] { new CreateOrderItemRequest(product.Id, 1) });

        using var response = await ApiTestData.PostOrderAsync(client, request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            "Order.InvalidCustomer");
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

        using var response = await ApiTestData.PostOrderAsync(client, request);

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

        using var response = await ApiTestData.PostOrderAsync(client, request);

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

        using var response = await ApiTestData.PostOrderAsync(client, request);

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

        using var response = await ApiTestData.PostOrderAsync(client, request);

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
        using var postResponse = await ApiTestData.PostOrderAsync(client, request);
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

    [Fact]
    public async Task Post_decrements_inventory_exposed_by_product_api()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 5);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        using var response = await ApiTestData.PostOrderAsync(client, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var productResponse = await client.GetAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);
        var updatedProduct = await productResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updatedProduct);
        Assert.Equal(3, updatedProduct.StockQuantity);
    }

    [Fact]
    public async Task Post_with_insufficient_stock_returns_conflict_without_changes()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 1);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        using var response = await ApiTestData.PostOrderAsync(client, request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "Product.InsufficientStock");
        using var productResponse = await client.GetAsync($"/api/products/{product.Id}");
        var unchangedProduct = await productResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(unchangedProduct);
        Assert.Equal(1, unchangedProduct.StockQuantity);
        await AssertPersistenceCountsAsync(factory, 0, 0);
    }

    [Fact]
    public async Task Post_with_one_unavailable_product_is_atomic()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var availableProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-AVAILABLE",
            "Available",
            10m,
            5);
        var unavailableProduct = await ApiTestData.CreateProductAsync(
            client,
            "SKU-UNAVAILABLE",
            "Unavailable",
            10m,
            0);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(availableProduct.Id, 2),
                new CreateOrderItemRequest(unavailableProduct.Id, 1)
            });

        using var response = await ApiTestData.PostOrderAsync(client, request);

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "Product.InsufficientStock");
        using var availableResponse = await client.GetAsync(
            $"/api/products/{availableProduct.Id}");
        using var unavailableResponse = await client.GetAsync(
            $"/api/products/{unavailableProduct.Id}");
        var returnedAvailable = await availableResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        var returnedUnavailable = await unavailableResponse.Content
            .ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(returnedAvailable);
        Assert.NotNull(returnedUnavailable);
        Assert.Equal(5, returnedAvailable.StockQuantity);
        Assert.Equal(0, returnedUnavailable.StockQuantity);
        await AssertPersistenceCountsAsync(factory, 0, 0);
    }

    [Fact]
    public async Task Failed_order_does_not_permanently_consume_idempotency_key()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var customer = await ApiTestData.CreateCustomerAsync(client);
        var product = await ApiTestData.CreateProductAsync(
            client,
            stockQuantity: 0);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 1) });
        const string idempotencyKey = "reusable-after-failure";

        using (var failedResponse = await ApiTestData.PostOrderAsync(
                   client,
                   request,
                   idempotencyKey))
        {
            await ApiTestAssertions.AssertProblemDetailsAsync(
                failedResponse,
                HttpStatusCode.Conflict,
                "Product.InsufficientStock");
        }

        await AssertPersistenceCountsAsync(factory, 0, 0);
        using (var stockScope = factory.Services.CreateScope())
        {
            var context = stockScope.ServiceProvider
                .GetRequiredService<EcommerceTxPrDbContext>();
            var trackedProduct = await context.Products.SingleAsync(
                candidate => candidate.Id == product.Id);
            trackedProduct.IncreaseStock(1);
            await context.SaveChangesAsync();
        }

        using var retryResponse = await ApiTestData.PostOrderAsync(
            client,
            request,
            idempotencyKey);

        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        await AssertPersistenceCountsAsync(factory, 1, 1);
        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(
            0,
            await verificationContext.Products
                .Where(candidate => candidate.Id == product.Id)
                .Select(candidate => candidate.StockQuantity)
                .SingleAsync());
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

    private static async Task AssertPersistenceCountsAsync(
        CustomerApiFactory factory,
        int expectedOrderCount,
        int expectedIdempotencyRecordCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<EcommerceTxPrDbContext>();
        Assert.Equal(expectedOrderCount, await context.Orders.CountAsync());
        Assert.Equal(
            expectedIdempotencyRecordCount,
            await context.OrderIdempotencyRecords.CountAsync());
    }
}
