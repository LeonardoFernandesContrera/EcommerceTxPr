using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
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
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<
                IOrderIdempotencyRepository,
                OrderIdempotencyRepository>();
            services.AddScoped<
                IDatabaseErrorClassifier,
                SqlServerDatabaseErrorClassifier>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
