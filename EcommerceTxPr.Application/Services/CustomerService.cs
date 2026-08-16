using EcommerceTxPr.Application.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Shared;

namespace EcommerceTxPr.Application.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public Task<Result<IEnumerable<Customer>, Error>> GetAllAsync()
        {
            return _customerRepository.GetAllAsync();
        }

        public Task<Result<Customer, Error>> GetByIdAsync(Guid id)
        {
            return _customerRepository.GetByIdAsync(id);
        }

        public Task<Result<string, Error>> CreateAsync(Customer obj)
        {
            return _customerRepository.CreateAsync(obj);
        }

        public Task<Result<string, Error>> UpdateAsync(Customer obj)
        {
            return _customerRepository.UpdateAsync(obj);
        }

        public Task<Result<string, Error>> DeleteByIdAsync(Guid id)
        {
            return _customerRepository.DeleteByIdAsync(id);
        }
    }
}
