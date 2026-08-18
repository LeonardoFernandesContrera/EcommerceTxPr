using EcommerceApi.V2.ErrorHandling;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
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
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService
            .CreateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        var order = result.Value!;

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            order);
    }
}
