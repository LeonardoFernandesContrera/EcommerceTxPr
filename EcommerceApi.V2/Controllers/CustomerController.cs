using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Customers.Services;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApi.V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController(ICustomerService customerService)
    {
        private readonly ICustomerService _customerService = customerService;

        [HttpGet]
        [Route("GetById")]
        public async Task<object> GetById(
            [FromQuery] Guid id,
            CancellationToken cancellationToken)
        {
            return await _customerService
                .GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);
        }

        [HttpGet]
        [Route("GetAll")]
        public async Task<object> GetAll(CancellationToken cancellationToken)
        {
            return await _customerService
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [HttpPost]
        [Route("Create")]
        public async Task<object> Create(
            [FromBody] CreateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            return await _customerService
                .CreateAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }

        [HttpPut]
        [Route("Update")]
        public async Task<object> Update(
            [FromQuery] Guid id,
            [FromBody] UpdateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            return await _customerService
                .UpdateAsync(id, request, cancellationToken)
                .ConfigureAwait(false);
        }

        [HttpDelete]
        [Route("Delete")]
        public async Task<object> Delete(
            [FromQuery] Guid id,
            CancellationToken cancellationToken)
        {
            return await _customerService
                .DeleteAsync(id, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
