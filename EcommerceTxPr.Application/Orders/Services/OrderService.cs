using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers;
using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Application.Orders.Contracts;
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
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
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

    public async Task<Result<OrderResponse, Error>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result<OrderResponse, Error>.Failure(
                OrderErrors.InvalidCustomer);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<OrderResponse, Error>.Failure(OrderErrors.Empty);
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty))
        {
            return Result<OrderResponse, Error>.Failure(OrderErrors.InvalidProduct);
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Result<OrderResponse, Error>.Failure(OrderErrors.InvalidQuantity);
        }

        var customer = await _customerRepository
            .GetByIdAsync(request.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        if (customer is null)
        {
            return Result<OrderResponse, Error>.Failure(CustomerErrors.NotFound);
        }

        var requestedQuantities = new Dictionary<Guid, int>();

        try
        {
            foreach (var item in request.Items)
            {
                requestedQuantities[item.ProductId] = requestedQuantities.TryGetValue(
                    item.ProductId,
                    out var currentQuantity)
                    ? checked(currentQuantity + item.Quantity)
                    : item.Quantity;
            }
        }
        catch (OverflowException)
        {
            return Result<OrderResponse, Error>.Failure(
                OrderErrors.InvalidQuantity);
        }

        var productIds = requestedQuantities.Keys.ToArray();

        var products = await _productRepository
            .GetByIdsAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        var productsById = products.ToDictionary(product => product.Id);

        if (productIds.Any(productId => !productsById.ContainsKey(productId)))
        {
            return Result<OrderResponse, Error>.Failure(ProductErrors.NotFound);
        }

        if (requestedQuantities.Any(requested =>
                productsById[requested.Key].StockQuantity < requested.Value))
        {
            return Result<OrderResponse, Error>.Failure(
                ProductErrors.InsufficientStock);
        }

        var order = new Order(customer.Id);

        foreach (var requestedItem in requestedQuantities)
        {
            var product = productsById[requestedItem.Key];
            order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                requestedItem.Value);
            product.DecreaseStock(requestedItem.Value);
        }

        order.Place();

        await _orderRepository
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

        var saveResult = await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saveResult == SaveChangesResult.ConcurrencyConflict)
        {
            return Result<OrderResponse, Error>.Failure(
                OrderErrors.InventoryChanged);
        }

        return Result<OrderResponse, Error>.Success(ToResponse(order));
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
