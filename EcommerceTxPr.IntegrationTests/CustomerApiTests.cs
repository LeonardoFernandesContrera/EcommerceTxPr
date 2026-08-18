using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.IntegrationTests.Infrastructure;

namespace EcommerceTxPr.IntegrationTests;

public sealed class CustomerApiTests
{
    [Fact]
    public async Task Health_returns_success()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetAll_with_no_customers_returns_empty_collection()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customers = await response.Content.ReadFromJsonAsync<CustomerResponse[]>();
        Assert.NotNull(customers);
        Assert.Empty(customers);
    }

    [Fact]
    public async Task Post_valid_customer_returns_created_response_and_location()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = ValidCreateRequest();

        using var response = await client.PostAsJsonAsync("/api/customers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(request.Name, customer.Name);
        Assert.Equal(request.BirthDate, customer.BirthDate);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var locationPath = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString;
        Assert.Equal($"/api/customers/{customer.Id}", locationPath);
    }

    [Fact]
    public async Task Post_followed_by_get_returns_created_customer()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await CreateCustomerAsync(client);

        using var response = await client.GetAsync($"/api/customers/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(returned);
        Assert.Equal(created.Id, returned.Id);
        Assert.Equal(created.Name, returned.Name);
        Assert.Equal(created.BirthDate, returned.BirthDate);
        Assert.Equal(created.CreationDate, returned.CreationDate);
    }

    [Fact]
    public async Task Put_existing_customer_updates_details_and_preserves_id()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await CreateCustomerAsync(client);
        var update = new UpdateCustomerRequest(
            "Updated Customer",
            new DateTime(1987, 6, 5, 0, 0, 0, DateTimeKind.Utc));

        using var response = await client.PutAsJsonAsync(
            $"/api/customers/{created.Id}",
            update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(returned);
        Assert.Equal(created.Id, returned.Id);
        Assert.Equal(update.Name, returned.Name);
        Assert.Equal(update.BirthDate, returned.BirthDate);
        Assert.Equal(created.CreationDate, returned.CreationDate);
    }

    [Fact]
    public async Task Put_unknown_customer_returns_not_found_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var update = new UpdateCustomerRequest(
            "Unknown Customer",
            new DateTime(1987, 6, 5, 0, 0, 0, DateTimeKind.Utc));

        using var response = await client.PutAsJsonAsync(
            $"/api/customers/{Guid.NewGuid()}",
            update);

        await AssertNotFoundProblemDetailsAsync(response);
    }

    [Fact]
    public async Task Delete_existing_customer_hides_it_from_item_and_collection_queries()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await CreateCustomerAsync(client);

        using var deleteResponse = await client.DeleteAsync(
            $"/api/customers/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getResponse = await client.GetAsync($"/api/customers/{created.Id}");
        await AssertNotFoundProblemDetailsAsync(getResponse);

        using var collectionResponse = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, collectionResponse.StatusCode);
        var customers = await collectionResponse.Content
            .ReadFromJsonAsync<CustomerResponse[]>();
        Assert.NotNull(customers);
        Assert.DoesNotContain(customers, customer => customer.Id == created.Id);
    }

    [Fact]
    public async Task Delete_unknown_customer_returns_not_found_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.DeleteAsync(
            $"/api/customers/{Guid.NewGuid()}");

        await AssertNotFoundProblemDetailsAsync(response);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Post_missing_or_blank_name_returns_validation_problem_details(
        string? name)
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new
        {
            name,
            birthDate = new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        using var response = await client.PostAsJsonAsync("/api/customers", request);

        await AssertValidationProblemDetailsAsync(response, "Name");
    }

    [Fact]
    public async Task Post_name_longer_than_maximum_returns_validation_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateCustomerRequest(
            new string('x', 61),
            new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        using var response = await client.PostAsJsonAsync("/api/customers", request);

        await AssertValidationProblemDetailsAsync(response, "Name");
    }

    [Fact]
    public async Task Post_default_birth_date_returns_validation_problem_details()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = new CreateCustomerRequest("Valid Name", DateTime.MinValue);

        using var response = await client.PostAsJsonAsync("/api/customers", request);

        await AssertValidationProblemDetailsAsync(response, "BirthDate");
    }

    private static CreateCustomerRequest ValidCreateRequest()
    {
        return new CreateCustomerRequest(
            "Created Customer",
            new DateTime(1992, 4, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            ValidCreateRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(customer);
        return customer;
    }

    private static async Task AssertNotFoundProblemDetailsAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal(
            "Customer.NotFound",
            root.GetProperty("code").GetString());
    }

    private static async Task AssertValidationProblemDetailsAsync(
        HttpResponseMessage response,
        string expectedField)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.GetProperty("errors").TryGetProperty(expectedField, out _));
    }
}
