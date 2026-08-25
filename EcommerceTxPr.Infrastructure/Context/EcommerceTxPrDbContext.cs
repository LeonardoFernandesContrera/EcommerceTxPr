using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Context
{
    public class EcommerceTxPrDbContext : DbContext
    {
        public EcommerceTxPrDbContext(DbContextOptions<EcommerceTxPrDbContext> options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Login> Logins { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderIdempotencyRecord> OrderIdempotencyRecords { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(EcommerceTxPrDbContext).Assembly);
        }
    }
}
