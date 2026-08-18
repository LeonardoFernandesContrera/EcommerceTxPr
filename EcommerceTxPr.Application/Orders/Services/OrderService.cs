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

    public OrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
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

        var productIds = request.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();

        var products = await _productRepository
            .GetByIdsAsync(productIds, cancellationToken)
            .ConfigureAwait(false);

        var productsById = products.ToDictionary(product => product.Id);

        if (productIds.Any(productId => !productsById.ContainsKey(productId)))
        {
            return Result<OrderResponse, Error>.Failure(ProductErrors.NotFound);
        }

        var order = new Order(customer.Id);

        foreach (var requestedItem in request.Items)
        {
            var product = productsById[requestedItem.ProductId];
            order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                requestedItem.Quantity);
        }

        order.Place();

        await _orderRepository
            .AddAsync(order, cancellationToken)
            .ConfigureAwait(false);

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
