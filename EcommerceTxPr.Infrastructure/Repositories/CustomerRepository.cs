using EcommerceTxPr.Application.Repositories;
using EcommerceTxPr.Domain.Entities;
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
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return "client created";
        }

        public async Task<Result<string, Error>> DeleteByIdAsync(Guid id)
        {
            var customer = await GetByIdAsync(id).ConfigureAwait(false);

            if (customer.IsSuccess == false)
                return GenericErrors.NotFoundObject;

            customer.Value!.TurnIsActiveToFalse();

            await UpdateAsync(customer.Value!).ConfigureAwait(false);

            return "Client was updated";
        }

        public async Task<Result<IEnumerable<Customer>, Error>> GetAllAsync()
        {
            return await _context.Customers
                .Where(customer => customer.IsActive)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Result<Customer, Error>> GetByIdAsync(Guid id)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(customer => customer.Id == id && customer.IsActive)
                .ConfigureAwait(false);

            if (customer == null)
                return GenericErrors.NotFoundObject;

            return customer;
        }

        public async Task<Result<string, Error>> UpdateAsync(Customer obj)
        {
            _context.Set<Customer>().Entry(obj).State = EntityState.Modified;
            await _context.SaveChangesAsync().ConfigureAwait(false);

            return "Modified with success";
        }
    }
}
