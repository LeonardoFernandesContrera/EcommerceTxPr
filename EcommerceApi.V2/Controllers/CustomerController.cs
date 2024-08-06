using EcommerceApi.Entities;
using EcommerceTxPr.Aplication.Services;
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
        public async Task<object> GetById(int Id)
        {
            return await _customerService.GetByIdAsync(Id).ConfigureAwait(false);
        }

        [HttpGet]
        [Route("GetAll")]
        public async Task<object> GetAll()
        {
            return await _customerService.GetAllAsync().ConfigureAwait(false);
        }

        [HttpPost]
        [Route("Create")]
        public async Task<object> Create(Customer customer)
        {
            return await _customerService.CreateAsync(customer).ConfigureAwait(false);
        }

        [HttpPut]
        [Route("Update")]
        public async Task<object> Update(Customer customer)
        {
            return await _customerService.UpdateAsync(customer).ConfigureAwait(false);
        }

        [HttpDelete]
        [Route("Delete")]
        public async Task<object> Delete(int Id)
        {
            return await _customerService.DeleteByIdAsync(Id).ConfigureAwait(false);
        }
    }
}
