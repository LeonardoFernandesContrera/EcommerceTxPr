using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories
{
    public sealed class CustomerRepository : ICustomerRepository
    {
        private readonly EcommerceTxPrDbContext _context;

        public CustomerRepository(EcommerceTxPrDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyCollection<Customer>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return await _context.Customers
                .AsNoTracking()
                .Where(customer => customer.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<Customer?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await _context.Customers
                .FirstOrDefaultAsync(
                    customer => customer.Id == id && customer.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task AddAsync(
            Customer customer,
            CancellationToken cancellationToken)
        {
            await _context.Customers
                .AddAsync(customer, cancellationToken)
                .ConfigureAwait(false);

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateAsync(
            Customer customer,
            CancellationToken cancellationToken)
        {
            if (_context.Entry(customer).State == EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "A customer must be loaded by this repository before it can be updated.");
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
