using EcommerceApi.V2.ErrorHandling;
using EcommerceTxPr.Application.Products.Contracts;
using EcommerceTxPr.Application.Products.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductService productService) : ControllerBase
{
    private readonly IProductService _productService = productService;

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ProductResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await _productService
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return Ok(result.Value!);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productService
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return Ok(result.Value!);
    }

    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService
            .CreateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        var product = result.Value!;

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService
            .UpdateAsync(id, request, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return Ok(result.Value!);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productService
            .DeleteAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return this.ToProblemDetails(result.Error!);
        }

        return NoContent();
    }
}
