using System.Net;
using System.Net.Http.Json;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Products.Contracts;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal static class ApiTestData
{
    public static async Task<CustomerResponse> CreateCustomerAsync(
        HttpClient client,
        string name = "Order Customer")
    {
        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            new CreateCustomerRequest(
                name,
                new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        return customer;
    }

    public static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        string sku = "SKU-001",
        string name = "Test Product",
        decimal price = 100m,
        int stockQuantity = 10)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(sku, name, price, stockQuantity));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        return product;
    }

    public static async Task<HttpResponseMessage> PostOrderAsync(
        HttpClient client,
        CreateOrderRequest request,
        string? idempotencyKey = "test-key",
        IReadOnlyCollection<string>? headerValues = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request)
        };

        if (headerValues is not null)
        {
            message.Headers.TryAddWithoutValidation(
                "Idempotency-Key",
                headerValues);
        }
        else if (idempotencyKey is not null)
        {
            message.Headers.TryAddWithoutValidation(
                "Idempotency-Key",
                idempotencyKey);
        }

        return await client.SendAsync(message);
    }
}
