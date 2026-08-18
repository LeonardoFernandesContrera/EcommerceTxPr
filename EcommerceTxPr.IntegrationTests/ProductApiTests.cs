using System.Net;
using System.Net.Http.Json;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.IntegrationTests.Infrastructure;

namespace EcommerceTxPr.IntegrationTests;

public sealed class ProductApiTests
{
    [Fact]
    public async Task Post_then_get_returns_created_product_and_location()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateProductRequest("SKU-001", "Product", 125.50m);

        using var postResponse = await client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(request.Sku, created.Sku);
        Assert.Equal(request.Name, created.Name);
        Assert.Equal(request.Price, created.Price);

        var location = postResponse.Headers.Location;
        Assert.NotNull(location);
        var locationPath = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString;
        Assert.Equal($"/api/products/{created.Id}", locationPath);

        using var getResponse = await client.GetAsync(locationPath);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var returned = await getResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(created, returned);
    }

    [Fact]
    public async Task Post_duplicate_sku_returns_conflict_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        await ApiTestData.CreateProductAsync(client);

        using var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("SKU-001", "Duplicate", 200m));

        await ApiTestAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.Conflict,
            "Product.DuplicateSku");
    }

    [Fact]
    public async Task Put_updates_editable_values_and_preserves_sku()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await ApiTestData.CreateProductAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"/api/products/{created.Id}",
            new UpdateProductRequest("Updated Product", 150m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.Sku, updated.Sku);
        Assert.Equal("Updated Product", updated.Name);
        Assert.Equal(150m, updated.Price);
    }

    [Fact]
    public async Task Delete_hides_product_from_item_and_collection_queries()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await ApiTestData.CreateProductAsync(client);

        using var deleteResponse = await client.DeleteAsync(
            $"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getResponse = await client.GetAsync($"/api/products/{created.Id}");
        await ApiTestAssertions.AssertProblemDetailsAsync(
            getResponse,
            HttpStatusCode.NotFound,
            "Product.NotFound");

        using var collectionResponse = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var products = await collectionResponse.Content
            .ReadFromJsonAsync<ProductResponse[]>();
        Assert.NotNull(products);
        Assert.DoesNotContain(products, product => product.Id == created.Id);
    }

    [Fact]
    public async Task Unknown_product_operations_return_not_found_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var productId = Guid.NewGuid();

        using var getResponse = await client.GetAsync($"/api/products/{productId}");
        await ApiTestAssertions.AssertProblemDetailsAsync(
            getResponse,
            HttpStatusCode.NotFound,
            "Product.NotFound");

        using var putResponse = await client.PutAsJsonAsync(
            $"/api/products/{productId}",
            new UpdateProductRequest("Missing", 10m));
        await ApiTestAssertions.AssertProblemDetailsAsync(
            putResponse,
            HttpStatusCode.NotFound,
            "Product.NotFound");

        using var deleteResponse = await client.DeleteAsync(
            $"/api/products/{productId}");
        await ApiTestAssertions.AssertProblemDetailsAsync(
            deleteResponse,
            HttpStatusCode.NotFound,
            "Product.NotFound");
    }
}
