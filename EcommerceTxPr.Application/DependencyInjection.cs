using EcommerceTxPr.Application.Customers.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceTxPr.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ICustomerService, CustomerService>();

            return services;
        }
    }
}
