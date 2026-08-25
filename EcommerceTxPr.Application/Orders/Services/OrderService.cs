using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers;
using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Products;
using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Orders.Services;

public sealed class OrderService : IOrderService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IOrderIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<OrderResponse, Error>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? Result<OrderResponse, Error>.Failure(OrderErrors.NotFound)
            : Result<OrderResponse, Error>.Success(ToResponse(order));
    }

    public async Task<Result<OrderCreationResponse, Error>> CreateAsync(
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var keyResult = OrderIdempotencyKey.Create(idempotencyKey);

        if (!keyResult.IsSuccess)
        {
            return Result<OrderCreationResponse, Error>.Failure(
                keyResult.Error!);
        }

        var fingerprintResult = OrderRequestFingerprint.Create(request);

        if (!fingerprintResult.IsSuccess)
        {
            return Result<OrderCreationResponse, Error>.Failure(
                fingerprintResult.Error!);
        }

        var key = keyResult.Value!;
        var normalizedRequest = fingerprintResult.Value!;
        var existingRecord = await _idempotencyRepository
            .GetByKeyHashAsync(key.KeyHash, cancellationToken)
            .ConfigureAwait(false);

        if (existingRecord is not null)
        {
            return await ReconcileAsync(
                    existingRecord,
                    normalizedRequest.RequestHash,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var customer = await _customerRepository
            .GetByIdAsync(normalizedRequest.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        if (customer is null)
        {
            return Result<OrderCreationResponse, Error>.Failure(
                CustomerErrors.NotFound);
        }

        var productIds = normalizedRequest.Items
            .Select(item => item.ProductId)
            .ToArray();

        var products = await _productRepository
            .GetByIdsAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        var productsById = products.ToDictionary(product => product.Id);

        if (productIds.Any(productId => !productsById.ContainsKey(productId)))
        {
            return Result<OrderCreationResponse, Error>.Failure(
                ProductErrors.NotFound);
        }

        if (normalizedRequest.Items.Any(requested =>
                productsById[requested.ProductId].StockQuantity
                    < requested.Quantity))
        {
            return Result<OrderCreationResponse, Error>.Failure(
                ProductErrors.InsufficientStock);
        }

        var order = new Order(customer.Id);

        foreach (var requestedItem in normalizedRequest.Items)
        {
            var product = productsById[requestedItem.ProductId];
            order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                requestedItem.Quantity);
            product.DecreaseStock(requestedItem.Quantity);
        }

        order.Place();

        await _orderRepository
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

        var idempotencyRecord = new OrderIdempotencyRecord(
            key.KeyHash,
            normalizedRequest.RequestHash,
            order.Id);

        await _idempotencyRepository
            .AddAsync(idempotencyRecord, cancellationToken)
            .ConfigureAwait(false);

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult is SaveChangesResult.ConcurrencyConflict
            or SaveChangesResult.IdempotencyConflict)
        {
            var winningRecord = await _idempotencyRepository
                .GetByKeyHashAsync(key.KeyHash, cancellationToken)
                .ConfigureAwait(false);

            if (winningRecord is null)
            {
                return Result<OrderCreationResponse, Error>.Failure(
                    OrderErrors.InventoryChanged);
            }

            return await ReconcileAsync(
                    winningRecord,
                    normalizedRequest.RequestHash,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<OrderCreationResponse, Error>.Success(
            new OrderCreationResponse(
                ToResponse(order),
                OrderCreationStatus.Created));
    }

    private async Task<Result<OrderCreationResponse, Error>> ReconcileAsync(
        OrderIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                record.RequestHash,
                requestHash,
                StringComparison.Ordinal))
        {
            return Result<OrderCreationResponse, Error>.Failure(
                OrderErrors.IdempotencyKeyConflict);
        }

        var order = await _orderRepository
            .GetByIdAsync(record.OrderId, cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? Result<OrderCreationResponse, Error>.Failure(OrderErrors.NotFound)
            : Result<OrderCreationResponse, Error>.Success(
                new OrderCreationResponse(
                    ToResponse(order),
                    OrderCreationStatus.Replayed));
    }

    private static OrderResponse ToResponse(Order order)
    {
        IReadOnlyCollection<OrderItemResponse> items = order.Items
            .Select(item => new OrderItemResponse(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.LineTotal))
            .ToArray();

        return new OrderResponse(
            order.Id,
            order.CustomerId,
            order.Status,
            order.CreationDate,
            order.Total,
            items);
    }
}
