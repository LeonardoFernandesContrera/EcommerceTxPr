using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly EcommerceTxPrDbContext _context;

    public PaymentRepository(EcommerceTxPrDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                payment => payment.Id == id,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Payment?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return await _context.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                payment => payment.OrderId == orderId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        await _context.Payments
            .AddAsync(payment, cancellationToken)
            .ConfigureAwait(false);
    }
}
