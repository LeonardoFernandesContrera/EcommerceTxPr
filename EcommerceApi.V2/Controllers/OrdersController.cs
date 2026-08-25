using EcommerceApi.V2.ErrorHandling;
using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IOrderService _orderService = orderService;

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _orderService
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return Ok(result.Value!);
    }

    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var keyValues = Request.Headers[IdempotencyKeyHeader];

        if (keyValues.Count > 1)
        {
            return this.ToProblemDetails(OrderErrors.IdempotencyKeyInvalid);
        }

        var result = await _orderService
            .CreateAsync(request, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        var creation = result.Value!;

        if (creation.Status == OrderCreationStatus.Replayed)
        {
            return Ok(creation.Order);
        }

        if (creation.Status != OrderCreationStatus.Created)
        {
            throw new InvalidOperationException(
                $"Unsupported order creation status: {creation.Status}.");
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = creation.Order.Id },
            creation.Order);
    }
}
