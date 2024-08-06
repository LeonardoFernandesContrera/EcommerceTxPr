using EcommerceApi.Entities;
using EcommerceTxPr.Domain.Shared;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.ResultPatterns;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly EcommerceTxPrDbContext _context;

        public CustomerRepository(EcommerceTxPrDbContext context)
        {
            _context = context;
        }

        public async Task<Result<string, Error>> CreateAsync(Customer obj)
        {
            await _context.Customers.AddAsync(obj).ConfigureAwait(false);

            return "client created";
        }

        public async Task<Result<string, Error>> DeleteByIdAsync(int Id)
        {
            var customer = await GetByIdAsync(Id).ConfigureAwait(false);

            if (customer.IsSuccess == false)
                return GenericErrors.NotFoundObject;

            customer.Value!.TurnIsActiveToFalse(); 

            await UpdateAsync(customer.Value!);

            return "Client was updated";
        }

        public async Task<Result<IEnumerable<Customer>, Error>> GetAllAsync()
        {
            var customers = await _context.Set<Customer>().ToListAsync();

            if (customers == null)
            {
                return GenericErrors.NotFoundObject;
            }
            else
                return customers;
        }

        public async Task<Result<Customer, Error>> GetByIdAsync(int Id)
        {
            var customer = await _context.Customers.FindAsync(Id);

            if (customer == null)
                return GenericErrors.NotFoundObject;

            return customer;
        }

        public async Task<Result<string, Error>> UpdateAsync(Customer obj)
        {
            _context.Set<Customer>().Entry(obj).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return "Modified with success";
        }
    }
}
