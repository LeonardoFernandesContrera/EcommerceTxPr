using EcommerceTxPr.Application.Common;
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
    public async Task CreateAsync_valid_request_decrements_aggregated_stock_and_commits_once()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var firstProduct = new Product("SKU-001", "First Product", 100m, 10);
        var secondProduct = new Product("SKU-002", "Second Product", 25m, 5);
        var customerRepository = new FakeCustomerRepository
        {
            GetByIdResult = customer
        };
        var productRepository = new FakeProductRepository
        {
            GetByIdsResult = new[] { firstProduct, secondProduct }
        };
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository,
            unitOfWork);
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
        Assert.Equal(7, firstProduct.StockQuantity);
        Assert.Equal(4, secondProduct.StockQuantity);
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
        Assert.Equal(1, unitOfWork.SaveChangesCalls);

        var requestedIds = Assert.Single(productRepository.GetByIdsRequests);
        Assert.Equal(2, requestedIds.Count);
        Assert.Contains(firstProduct.Id, requestedIds);
        Assert.Contains(secondProduct.Id, requestedIds);
    }

    [Fact]
    public async Task CreateAsync_empty_customer_id_returns_validation_without_querying_or_committing()
    {
        var productRepository = new FakeProductRepository();
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository(),
            productRepository,
            orderRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(Guid.Empty, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidCustomer, result.Error);
        Assert.Empty(productRepository.GetByIdsRequests);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_missing_customer_does_not_change_stock_or_commit()
    {
        var product = new Product("SKU-001", "Product", 10m, 5);
        var productRepository = new FakeProductRepository
        {
            GetByIdsResult = new[] { product }
        };
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository(),
            productRepository,
            orderRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(Guid.NewGuid(), product.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CustomerErrors.NotFound, result.Error);
        Assert.Equal(5, product.StockQuantity);
        Assert.Empty(productRepository.GetByIdsRequests);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_missing_product_does_not_change_other_stock_or_commit()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var existingProduct = new Product("SKU-001", "Product", 10m, 5);
        var missingProductId = Guid.NewGuid();
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { existingProduct } },
            orderRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(existingProduct.Id, 1),
                new CreateOrderItemRequest(missingProductId, 1)
            });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.NotFound, result.Error);
        Assert.Equal(5, existingProduct.StockQuantity);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_duplicate_quantities_are_aggregated_before_stock_validation()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 4);
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(product.Id, 2),
                new CreateOrderItemRequest(product.Id, 3)
            });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.InsufficientStock, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(4, product.StockQuantity);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_insufficient_stock_validates_all_products_before_mutation()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var availableProduct = new Product("SKU-001", "Available", 10m, 5);
        var unavailableProduct = new Product("SKU-002", "Unavailable", 10m, 0);
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository
            {
                GetByIdsResult = new[] { availableProduct, unavailableProduct }
            },
            orderRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(availableProduct.Id, 2),
                new CreateOrderItemRequest(unavailableProduct.Id, 1)
            });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.InsufficientStock, result.Error);
        Assert.Equal(5, availableProduct.StockQuantity);
        Assert.Equal(0, unavailableProduct.StockQuantity);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_concurrency_commit_failure_returns_inventory_changed_conflict()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 1);
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork
        {
            Result = SaveChangesResult.ConcurrencyConflict
        };
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(customer.Id, product.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(OrderErrors.InventoryChanged, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Single(orderRepository.AddedOrders);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_empty_items_returns_validation_without_adding_or_committing()
    {
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository(),
            new FakeProductRepository(),
            orderRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), Array.Empty<CreateOrderItemRequest>()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.Empty, result.Error);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_invalid_quantity_returns_validation_without_adding_or_committing()
    {
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository(),
            new FakeProductRepository(),
            orderRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 0) });

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static CreateOrderRequest ValidRequest(Guid customerId, Guid productId)
    {
        return new CreateOrderRequest(
            customerId,
            new[] { new CreateOrderItemRequest(productId, 1) });
    }
}
