using System.Net;
using System.Text.Json;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Customers.Services;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class GlobalExceptionHandlerTests
{
    private const string InternalExceptionMessage =
        "Sensitive test-only exception information.";

    [Fact]
    public async Task Unexpected_exception_returns_safe_problem_details()
    {
        using var factory = new CustomerApiFactory(services =>
        {
            services.RemoveAll<ICustomerService>();
            services.AddScoped<ICustomerService>(_ => new ThrowingCustomerService());
        });
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InternalExceptionMessage, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("Internal Server Error", root.GetProperty("title").GetString());
        Assert.Equal(
            "An unexpected error occurred while processing the request.",
            root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("traceId").GetString()));
    }

    private sealed class ThrowingCustomerService : ICustomerService
    {
        public Task<Result<IReadOnlyCollection<CustomerResponse>, Error>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(InternalExceptionMessage);
        }

        public Task<Result<CustomerResponse, Error>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<CustomerResponse, Error>> CreateAsync(
            CreateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<CustomerResponse, Error>> UpdateAsync(
            Guid id,
            UpdateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid, Error>> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
