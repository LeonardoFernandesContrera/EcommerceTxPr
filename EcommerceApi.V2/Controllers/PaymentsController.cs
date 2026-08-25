using EcommerceApi.V2.ErrorHandling;
using EcommerceTxPr.Application.Payments.Contracts;
using EcommerceTxPr.Application.Payments.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}")]
public sealed class PaymentsController(IPaymentService paymentService)
    : ControllerBase
{
    private readonly IPaymentService _paymentService = paymentService;

    [HttpPost("payments")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Process(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService
            .ProcessPaymentAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return CreatedAtAction(
            nameof(GetByOrderId),
            new { orderId },
            result.Value!);
    }

    [HttpGet("payment")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetByOrderId(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService
            .GetByOrderIdAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return Ok(result.Value!);
    }
}
