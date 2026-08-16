using EcommerceApi.V2.ErrorHandling;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Customers.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomerController(ICustomerService customerService) : ControllerBase
    {
        private readonly ICustomerService _customerService = customerService;

        [HttpGet]
        [ProducesResponseType<IReadOnlyCollection<CustomerResponse>>(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyCollection<CustomerResponse>>> GetAll(
            CancellationToken cancellationToken)
        {
            var result = await _customerService
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return this.ToProblemDetails(result.Error!);
            }

            return Ok(result.Value!);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CustomerResponse>> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _customerService
                .GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return this.ToProblemDetails(result.Error!);
            }

            return Ok(result.Value!);
        }

        [HttpPost]
        [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CustomerResponse>> Create(
            [FromBody] CreateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _customerService
                .CreateAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return this.ToProblemDetails(result.Error!);
            }

            var customer = result.Value!;

            return CreatedAtAction(
                nameof(GetById),
                new { id = customer.Id },
                customer);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CustomerResponse>> Update(
            Guid id,
            [FromBody] UpdateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _customerService
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
            var result = await _customerService
                .DeleteAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                return this.ToProblemDetails(result.Error!);
            }

            return NoContent();
        }
    }
}
