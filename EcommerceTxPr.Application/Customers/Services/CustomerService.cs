using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers.Contracts;
using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Domain.Entities;

namespace EcommerceTxPr.Application.Customers.Services
{
    public sealed class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerService(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<Result<IReadOnlyCollection<CustomerResponse>, Error>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            var customers = await _customerRepository
                .GetAllAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyCollection<CustomerResponse> response = customers
                .Select(ToResponse)
                .ToArray();

            return Result<IReadOnlyCollection<CustomerResponse>, Error>.Success(response);
        }

        public async Task<Result<CustomerResponse, Error>> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var customer = await _customerRepository
                .GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (customer is null)
            {
                return Result<CustomerResponse, Error>.Failure(CustomerErrors.NotFound);
            }

            return Result<CustomerResponse, Error>.Success(ToResponse(customer));
        }

        public async Task<Result<CustomerResponse, Error>> CreateAsync(
            CreateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            var customer = new Customer(request.Name, request.BirthDate);

            await _customerRepository
                .AddAsync(customer, cancellationToken)
                .ConfigureAwait(false);

            return Result<CustomerResponse, Error>.Success(ToResponse(customer));
        }

        public async Task<Result<CustomerResponse, Error>> UpdateAsync(
            Guid id,
            UpdateCustomerRequest request,
            CancellationToken cancellationToken)
        {
            var customer = await _customerRepository
                .GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (customer is null)
            {
                return Result<CustomerResponse, Error>.Failure(CustomerErrors.NotFound);
            }

            customer.UpdateDetails(request.Name, request.BirthDate);

            await _customerRepository
                .UpdateAsync(customer, cancellationToken)
                .ConfigureAwait(false);

            return Result<CustomerResponse, Error>.Success(ToResponse(customer));
        }

        public async Task<Result<Guid, Error>> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var customer = await _customerRepository
                .GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);

            if (customer is null)
            {
                return Result<Guid, Error>.Failure(CustomerErrors.NotFound);
            }

            customer.Deactivate();

            await _customerRepository
                .UpdateAsync(customer, cancellationToken)
                .ConfigureAwait(false);

            return Result<Guid, Error>.Success(customer.Id);
        }

        private static CustomerResponse ToResponse(Customer customer)
        {
            return new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.BirthDate,
                customer.CreationDate);
        }
    }
}
