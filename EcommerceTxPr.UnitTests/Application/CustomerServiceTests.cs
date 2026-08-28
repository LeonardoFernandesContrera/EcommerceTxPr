using EcommerceTxPr.Application.Common;
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
        var service = new CustomerService(repository, new FakeUnitOfWork());

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
        var service = new CustomerService(repository, new FakeUnitOfWork());

        var result = await service.GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        AssertMatches(customer, result.Value);
    }

    [Fact]
    public async Task GetByIdAsync_missing_customer_returns_not_found()
    {
        var service = new CustomerService(
            new FakeCustomerRepository(),
            new FakeUnitOfWork());

        var result = await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task CreateAsync_adds_customer_and_returns_created_response()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);
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
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_existing_customer_updates_details_and_preserves_identity()
    {
        var customer = new Customer("Original Name", new DateTime(1982, 2, 3));
        var originalId = customer.Id;
        var originalCreationDate = customer.CreationDate;
        var repository = new FakeCustomerRepository { GetByIdResult = customer };
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);
        var request = new UpdateCustomerRequest(
            "Updated Name",
            new DateTime(1983, 4, 5));

        var result = await service.UpdateAsync(
            customer.Id,
            request,
            CancellationToken.None);

        Assert.Equal(request.Name, customer.Name);
        Assert.Equal(request.BirthDate, customer.BirthDate);
        Assert.Equal(originalId, customer.Id);
        Assert.Equal(originalCreationDate, customer.CreationDate);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        AssertMatches(customer, result.Value);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_missing_customer_returns_not_found_without_updating()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateCustomerRequest("Updated Name", new DateTime(1983, 4, 5)),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_existing_customer_deactivates_and_updates_customer()
    {
        var customer = new Customer("Customer To Delete", new DateTime(1975, 8, 9));
        var repository = new FakeCustomerRepository { GetByIdResult = customer };
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);

        var result = await service.DeleteAsync(customer.Id, CancellationToken.None);

        Assert.False(customer.IsActive);
        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_missing_customer_returns_not_found_without_updating()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new CustomerService(repository, unitOfWork);

        var result = await service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_unknown_save_result_fails_closed()
    {
        var unitOfWork = new FakeUnitOfWork
        {
            Result = (SaveChangesResult)999
        };
        var service = new CustomerService(
            new FakeCustomerRepository(),
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(
                new CreateCustomerRequest(
                    "Customer",
                    new DateTime(1990, 1, 1)),
                CancellationToken.None));

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_unknown_save_result_fails_closed()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var unitOfWork = new FakeUnitOfWork
        {
            Result = (SaveChangesResult)999
        };
        var service = new CustomerService(
            new FakeCustomerRepository { GetByIdResult = customer },
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                customer.Id,
                new UpdateCustomerRequest(
                    "Updated",
                    new DateTime(1991, 2, 2)),
                CancellationToken.None));

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_unknown_save_result_fails_closed()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var unitOfWork = new FakeUnitOfWork
        {
            Result = (SaveChangesResult)999
        };
        var service = new CustomerService(
            new FakeCustomerRepository { GetByIdResult = customer },
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(customer.Id, CancellationToken.None));

        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    private static void AssertMatches(Customer customer, CustomerResponse response)
    {
        Assert.Equal(customer.Id, response.Id);
        Assert.Equal(customer.Name, response.Name);
        Assert.Equal(customer.BirthDate, response.BirthDate);
        Assert.Equal(customer.CreationDate, response.CreationDate);
    }
}
