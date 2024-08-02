using EcommerceApi.Entities;
using EcommerceTxPr.Domain.Shared;
using EcommerceTxPr.Infrastructure.Repositories;

namespace EcommerceTxPr.Aplication.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<Result<IEnumerable<Customer>, Error>> GetAllAsync()
        {
            return await _customerRepository.GetAllAsync();
        }

        public async Task<Result<Customer, Error>> GetByIdAsync(int Id)
        {
            return await _customerRepository.GetByIdAsync(Id);
        }

        public async Task<Result<string, Error>> CreateAsync(Customer obj)
        {
            return await _customerRepository.CreateAsync(obj);
        }

        public async Task<Result<string, Error>> UpdateAsync(Customer obj)
        {
            return await _customerRepository.UpdateAsync(obj);
        }

        public async Task<Result<string, Error>> DeleteByIdAsync(int Id)
        {
            return await _customerRepository.DeleteByIdAsync(Id);
        }

    }
}
