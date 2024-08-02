using EcommerceApi.Entities;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
