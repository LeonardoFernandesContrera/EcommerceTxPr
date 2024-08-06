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

        public Task<Result<IEnumerable<Customer>, Error>> GetAllAsync()
        {
            return _customerRepository.GetAllAsync();
        }

        public Task<Result<Customer, Error>> GetByIdAsync(int Id)
        {
            return _customerRepository.GetByIdAsync(Id);
        }

        public  Task<Result<string, Error>> CreateAsync(Customer obj)
        {
            return _customerRepository.CreateAsync(obj);
        }

        public  Task<Result<string, Error>> UpdateAsync(Customer obj)
        {
            return _customerRepository.UpdateAsync(obj);
        }

        public  Task<Result<string, Error>> DeleteByIdAsync(int Id)
        {
            return _customerRepository.DeleteByIdAsync(Id);
        }

    }
}
