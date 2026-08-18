using EcommerceTxPr.Application.Customers;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Customers.Services;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task GetAllAsync_returns_mapped_customer_responses()
    {
        var first = new Customer("First Customer", new DateTime(1980, 1, 2));
        var second = new Customer("Second Customer", new DateTime(1990, 3, 4));
        var repository = new FakeCustomerRepository
        {
            GetAllResult = new[] { first, second }
        };
        var service = new CustomerService(repository);

        var result = await service.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Collection(
            result.Value,
            response => AssertMatches(first, response),
            response => AssertMatches(second, response));
    }

    [Fact]
    public async Task GetByIdAsync_existing_customer_returns_success()
    {
        var customer = new Customer("Existing Customer", new DateTime(1988, 6, 15));
        var repository = new FakeCustomerRepository { GetByIdResult = customer };
        var service = new CustomerService(repository);

        var result = await service.GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        AssertMatches(customer, result.Value);
    }

    [Fact]
    public async Task GetByIdAsync_missing_customer_returns_not_found()
    {
        var service = new CustomerService(new FakeCustomerRepository());

        var result = await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task CreateAsync_adds_customer_and_returns_created_response()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);
        var request = new CreateCustomerRequest(
            "Created Customer",
            new DateTime(1995, 7, 20));

        var result = await service.CreateAsync(request, CancellationToken.None);

        var addedCustomer = Assert.Single(repository.AddedCustomers);
        Assert.Equal(request.Name, addedCustomer.Name);
        Assert.Equal(request.BirthDate, addedCustomer.BirthDate);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        AssertMatches(addedCustomer, result.Value);
    }

    [Fact]
    public async Task UpdateAsync_existing_customer_updates_details_and_preserves_identity()
    {
        var customer = new Customer("Original Name", new DateTime(1982, 2, 3));
        var originalId = customer.Id;
        var originalCreationDate = customer.CreationDate;
        var repository = new FakeCustomerRepository { GetByIdResult = customer };
        var service = new CustomerService(repository);
        var request = new UpdateCustomerRequest(
            "Updated Name",
            new DateTime(1983, 4, 5));

        var result = await service.UpdateAsync(
            customer.Id,
            request,
            CancellationToken.None);

        var updatedCustomer = Assert.Single(repository.UpdatedCustomers);
        Assert.Same(customer, updatedCustomer);
        Assert.Equal(request.Name, updatedCustomer.Name);
        Assert.Equal(request.BirthDate, updatedCustomer.BirthDate);
        Assert.Equal(originalId, updatedCustomer.Id);
        Assert.Equal(originalCreationDate, updatedCustomer.CreationDate);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        AssertMatches(updatedCustomer, result.Value);
    }

    [Fact]
    public async Task UpdateAsync_missing_customer_returns_not_found_without_updating()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateCustomerRequest("Updated Name", new DateTime(1983, 4, 5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Empty(repository.UpdatedCustomers);
    }

    [Fact]
    public async Task DeleteAsync_existing_customer_deactivates_and_updates_customer()
    {
        var customer = new Customer("Customer To Delete", new DateTime(1975, 8, 9));
        var repository = new FakeCustomerRepository { GetByIdResult = customer };
        var service = new CustomerService(repository);

        var result = await service.DeleteAsync(customer.Id, CancellationToken.None);

        var updatedCustomer = Assert.Single(repository.UpdatedCustomers);
        Assert.Same(customer, updatedCustomer);
        Assert.False(customer.IsActive);
        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value);
    }

    [Fact]
    public async Task DeleteAsync_missing_customer_returns_not_found_without_updating()
    {
        var repository = new FakeCustomerRepository();
        var service = new CustomerService(repository);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Empty(repository.UpdatedCustomers);
    }

    private static void AssertMatches(Customer customer, CustomerResponse response)
    {
        Assert.Equal(customer.Id, response.Id);
        Assert.Equal(customer.Name, response.Name);
        Assert.Equal(customer.BirthDate, response.BirthDate);
        Assert.Equal(customer.CreationDate, response.CreationDate);
    }
}
