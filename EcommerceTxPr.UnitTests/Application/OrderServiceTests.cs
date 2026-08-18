using EcommerceTxPr.Application.Customers;
using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Services;
using EcommerceTxPr.Application.Products;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_valid_request_uses_product_snapshots_and_adds_pending_order()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var firstProduct = new Product("SKU-001", "First Product", 100m);
        var secondProduct = new Product("SKU-002", "Second Product", 25m);
        var customerRepository = new FakeCustomerRepository
        {
            GetByIdResult = customer
        };
        var productRepository = new FakeProductRepository
        {
            GetByIdsResult = new[] { firstProduct, secondProduct }
        };
        var orderRepository = new FakeOrderRepository();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(firstProduct.Id, 2),
                new CreateOrderItemRequest(firstProduct.Id, 1),
                new CreateOrderItemRequest(secondProduct.Id, 1)
            });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = Assert.Single(orderRepository.AddedOrders);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(325m, order.Total);
        Assert.Collection(
            order.Items.OrderBy(item => item.ProductName),
            item =>
            {
                Assert.Equal("First Product", item.ProductName);
                Assert.Equal(100m, item.UnitPrice);
                Assert.Equal(3, item.Quantity);
                Assert.Equal(300m, item.LineTotal);
            },
            item =>
            {
                Assert.Equal("Second Product", item.ProductName);
                Assert.Equal(25m, item.UnitPrice);
                Assert.Equal(1, item.Quantity);
                Assert.Equal(25m, item.LineTotal);
            });
        Assert.Equal(order.Id, result.Value?.Id);
        Assert.Equal(325m, result.Value?.Total);

        var requestedIds = Assert.Single(productRepository.GetByIdsRequests);
        Assert.Equal(2, requestedIds.Count);
        Assert.Contains(firstProduct.Id, requestedIds);
        Assert.Contains(secondProduct.Id, requestedIds);
    }

    [Fact]
    public async Task CreateAsync_missing_customer_returns_not_found_without_adding_order()
    {
        var productRepository = new FakeProductRepository();
        var orderRepository = new FakeOrderRepository();
        var service = new OrderService(
            new FakeCustomerRepository(),
            productRepository,
            orderRepository);

        var result = await service.CreateAsync(
            ValidRequest(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Empty(productRepository.GetByIdsRequests);
        Assert.Empty(orderRepository.AddedOrders);
    }

    [Fact]
    public async Task CreateAsync_missing_product_returns_not_found_without_adding_order()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var orderRepository = new FakeOrderRepository();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository(),
            orderRepository);

        var result = await service.CreateAsync(
            ValidRequest(customer.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Empty(orderRepository.AddedOrders);
    }

    [Fact]
    public async Task CreateAsync_empty_items_returns_validation_without_adding_order()
    {
        var orderRepository = new FakeOrderRepository();
        var service = new OrderService(
            new FakeCustomerRepository(),
            new FakeProductRepository(),
            orderRepository);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), Array.Empty<CreateOrderItemRequest>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.Empty, result.Error);
        Assert.Empty(orderRepository.AddedOrders);
    }

    [Fact]
    public async Task CreateAsync_invalid_quantity_returns_validation_without_adding_order()
    {
        var orderRepository = new FakeOrderRepository();
        var service = new OrderService(
            new FakeCustomerRepository(),
            new FakeProductRepository(),
            orderRepository);
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 0) });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
        Assert.Empty(orderRepository.AddedOrders);
    }

    private static CreateOrderRequest ValidRequest(Guid customerId, Guid productId)
    {
        return new CreateOrderRequest(
            customerId,
            new[] { new CreateOrderItemRequest(productId, 1) });
    }
}
