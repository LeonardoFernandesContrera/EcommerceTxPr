using EcommerceTxPr.Aplication.Services;
using EcommerceTxPr.Infrastructure.Repositories;

namespace EcommerceApi.V2.Configuration
{
    public static class ConfigureDI
    {
        public static IServiceCollection ConfigureDIExtension(this IServiceCollection services)
        {
            // Services
            services.AddScoped<ICustomerService, CustomerService>();


            // Repositories
            services.AddScoped<ICustomerRepository, CustomerRepository>();

            return services;
        }

    }
}
