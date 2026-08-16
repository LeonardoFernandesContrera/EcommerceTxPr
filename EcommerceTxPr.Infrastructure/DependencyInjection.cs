using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddDbContext<EcommerceTxPrDbContext>(
                options => options.UseSqlServer(connectionString));
            services.AddScoped<ICustomerRepository, CustomerRepository>();

            return services;
        }
    }
}
