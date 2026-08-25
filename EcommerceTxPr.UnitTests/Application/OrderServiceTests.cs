using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers;
using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Application.Orders.Services;
using EcommerceTxPr.Application.Products;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;
using EcommerceTxPr.UnitTests.TestDoubles;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_consumes_stock_stages_order_and_commits_once()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 5);
        var orderRepository = new FakeOrderRepository();
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            idempotencyRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderCreationStatus.Created, result.Value?.Status);
        Assert.Equal(3, product.StockQuantity);
        var order = Assert.Single(orderRepository.AddedOrders);
        var idempotencyRecord = Assert.Single(
            idempotencyRepository.AddedRecords);
        Assert.Equal(order.Id, idempotencyRecord.OrderId);
        Assert.Equal(64, idempotencyRecord.KeyHash.Length);
        Assert.Equal(64, idempotencyRecord.RequestHash.Length);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_same_key_and_request_replays_without_mutating_or_committing()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 5);
        var customerRepository = new FakeCustomerRepository
        {
            GetByIdResult = customer
        };
        var productRepository = new FakeProductRepository
        {
            GetByIdsResult = new[] { product }
        };
        var orderRepository = new FakeOrderRepository();
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository,
            idempotencyRepository,
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        var created = await service.CreateAsync(
            request,
            "same-key",
            CancellationToken.None);
        var persistedOrder = Assert.Single(orderRepository.AddedOrders);
        orderRepository.GetByIdResult = persistedOrder;
        idempotencyRepository.EnqueueGetResult(
            Assert.Single(idempotencyRepository.AddedRecords));

        var replayed = await service.CreateAsync(
            request,
            "same-key",
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(OrderCreationStatus.Created, created.Value?.Status);
        Assert.Equal(OrderCreationStatus.Replayed, replayed.Value?.Status);
        Assert.Equal(created.Value?.Order.Id, replayed.Value?.Order.Id);
        Assert.Equal(3, product.StockQuantity);
        Assert.Single(orderRepository.AddedOrders);
        Assert.Single(idempotencyRepository.AddedRecords);
        Assert.Single(customerRepository.GetByIdRequests);
        Assert.Single(productRepository.GetByIdsRequests);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_same_key_and_different_request_returns_conflict_without_second_mutation()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 5);
        var customerRepository = new FakeCustomerRepository
        {
            GetByIdResult = customer
        };
        var productRepository = new FakeProductRepository
        {
            GetByIdsResult = new[] { product }
        };
        var orderRepository = new FakeOrderRepository();
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository,
            idempotencyRepository,
            unitOfWork);
        var firstRequest = ValidRequest(customer.Id, product.Id);

        var created = await service.CreateAsync(
            firstRequest,
            "same-key",
            CancellationToken.None);
        idempotencyRepository.EnqueueGetResult(
            Assert.Single(idempotencyRepository.AddedRecords));
        var conflictingRequest = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 2) });

        var conflict = await service.CreateAsync(
            conflictingRequest,
            "same-key",
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.False(conflict.IsSuccess);
        Assert.Equal(OrderErrors.IdempotencyKeyConflict, conflict.Error);
        Assert.Equal(4, product.StockQuantity);
        Assert.Single(orderRepository.AddedOrders);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(SaveChangesResult.ConcurrencyConflict)]
    [InlineData(SaveChangesResult.IdempotencyConflict)]
    public async Task CreateAsync_commit_conflict_with_matching_winner_replays(
        SaveChangesResult saveResult)
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 1);
        var request = ValidRequest(customer.Id, product.Id);
        var normalizedRequest = OrderRequestFingerprint.Create(request).Value!;
        var key = OrderIdempotencyKey.Create("same-key").Value!;
        var winningOrder = CreatePlacedOrder(customer.Id, product);
        var winningRecord = new OrderIdempotencyRecord(
            key.KeyHash,
            normalizedRequest.RequestHash,
            winningOrder.Id);
        var orderRepository = new FakeOrderRepository
        {
            GetByIdResult = winningOrder
        };
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        idempotencyRepository.EnqueueGetResult(null);
        idempotencyRepository.EnqueueGetResult(winningRecord);
        var unitOfWork = new FakeUnitOfWork { Result = saveResult };
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            idempotencyRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            request,
            "same-key",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderCreationStatus.Replayed, result.Value?.Status);
        Assert.Equal(winningOrder.Id, result.Value?.Order.Id);
        Assert.Single(orderRepository.AddedOrders);
        Assert.Equal(new[] { winningOrder.Id }, orderRepository.GetByIdRequests);
        Assert.Equal(2, idempotencyRepository.GetByKeyHashRequests.Count);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(SaveChangesResult.ConcurrencyConflict)]
    [InlineData(SaveChangesResult.IdempotencyConflict)]
    public async Task CreateAsync_commit_conflict_without_winner_returns_inventory_changed(
        SaveChangesResult saveResult)
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 1);
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        idempotencyRepository.EnqueueGetResult(null);
        idempotencyRepository.EnqueueGetResult(null);
        var unitOfWork = new FakeUnitOfWork { Result = saveResult };
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            new FakeOrderRepository(),
            idempotencyRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(customer.Id, product.Id),
            "same-key",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InventoryChanged, result.Error);
        Assert.Equal(2, idempotencyRepository.GetByKeyHashRequests.Count);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_winner_committed_before_stock_validation_replays_instead_of_failing_stock()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var exhaustedProduct = new Product("SKU-001", "Product", 10m, 0);
        var request = ValidRequest(customer.Id, exhaustedProduct.Id);
        var normalizedRequest = OrderRequestFingerprint.Create(request).Value!;
        var key = OrderIdempotencyKey.Create("race-key").Value!;
        var winningOrder = CreatePlacedOrder(customer.Id, exhaustedProduct);
        var winningRecord = new OrderIdempotencyRecord(
            key.KeyHash,
            normalizedRequest.RequestHash,
            winningOrder.Id);
        var orderRepository = new FakeOrderRepository
        {
            GetByIdResult = winningOrder
        };
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        idempotencyRepository.EnqueueGetResult(null);
        idempotencyRepository.EnqueueGetResult(winningRecord);
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository
            {
                GetByIdsResult = new[] { exhaustedProduct }
            },
            orderRepository,
            idempotencyRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            request,
            "race-key",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderCreationStatus.Replayed, result.Value?.Status);
        Assert.Equal(winningOrder.Id, result.Value?.Order.Id);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(2, idempotencyRepository.GetByKeyHashRequests.Count);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_invalid_key_returns_validation_before_any_repository_access()
    {
        var customerRepository = new FakeCustomerRepository();
        var productRepository = new FakeProductRepository();
        var orderRepository = new FakeOrderRepository();
        var idempotencyRepository = new FakeOrderIdempotencyRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository,
            idempotencyRepository,
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(Guid.NewGuid(), Guid.NewGuid()),
            "   ",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.IdempotencyKeyRequired, result.Error);
        Assert.Empty(idempotencyRepository.GetByKeyHashRequests);
        Assert.Empty(customerRepository.GetByIdRequests);
        Assert.Empty(productRepository.GetByIdsRequests);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_duplicate_product_quantities_merge_and_consume_total_stock()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 6);
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(product.Id, 2),
                new CreateOrderItemRequest(product.Id, 3)
            });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, product.StockQuantity);
        var order = Assert.Single(orderRepository.AddedOrders);
        Assert.Equal(5, Assert.Single(order.Items).Quantity);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateAsync_insufficient_stock_does_not_mutate_stage_or_commit()
    {
        var customer = new Customer("Customer", new DateTime(1990, 1, 1));
        var product = new Product("SKU-001", "Product", 10m, 2);
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new OrderService(
            new FakeCustomerRepository { GetByIdResult = customer },
            new FakeProductRepository { GetByIdsResult = new[] { product } },
            orderRepository,
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[] { new CreateOrderItemRequest(product.Id, 3) });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductErrors.InsufficientStock, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error?.Type);
        Assert.Equal(2, product.StockQuantity);
        Assert.Empty(orderRepository.AddedOrders);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(firstProduct.Id, 2),
                new CreateOrderItemRequest(firstProduct.Id, 1),
                new CreateOrderItemRequest(secondProduct.Id, 1)
            });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

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
        Assert.Equal(order.Id, result.Value?.Order.Id);
        Assert.Equal(325m, result.Value?.Order.Total);
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
        var customerRepository = new FakeCustomerRepository();
        var service = new OrderService(
            customerRepository,
            productRepository,
            orderRepository,
            new FakeOrderIdempotencyRepository(),
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(Guid.Empty, Guid.NewGuid()),
            "test-key",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidCustomer, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Empty(customerRepository.GetByIdRequests);
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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(Guid.NewGuid(), product.Id),
            "test-key",
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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(existingProduct.Id, 1),
                new CreateOrderItemRequest(missingProductId, 1)
            });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(product.Id, 2),
                new CreateOrderItemRequest(product.Id, 3)
            });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            customer.Id,
            new[]
            {
                new CreateOrderItemRequest(availableProduct.Id, 2),
                new CreateOrderItemRequest(unavailableProduct.Id, 1)
            });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);

        var result = await service.CreateAsync(
            ValidRequest(customer.Id, product.Id),
            "test-key",
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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);

        var result = await service.CreateAsync(
            new CreateOrderRequest(Guid.NewGuid(), Array.Empty<CreateOrderItemRequest>()),
            "test-key",
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
            new FakeOrderIdempotencyRepository(),
            unitOfWork);
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 0) });

        var result = await service.CreateAsync(
            request,
            "test-key",
            CancellationToken.None);

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

    private static Order CreatePlacedOrder(Guid customerId, Product product)
    {
        var order = new Order(customerId);
        order.AddItem(product.Id, product.Name, product.Price, 1);
        order.Place();
        return order;
    }
}
