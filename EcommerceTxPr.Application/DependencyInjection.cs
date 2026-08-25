using EcommerceTxPr.Application.Customers.Services;
using EcommerceTxPr.Application.Orders.Services;
using EcommerceTxPr.Application.Payments.Services;
using EcommerceTxPr.Application.Products.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}
