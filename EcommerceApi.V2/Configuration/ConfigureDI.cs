using EcommerceTxPr.Application.Services;

namespace EcommerceApi.V2.Configuration
{
    public static class ConfigureDI
    {
        public static IServiceCollection ConfigureDIExtension(this IServiceCollection services)
        {
            // Services
            services.AddScoped<ICustomerService, CustomerService>();
            return services;
        }

    }
}
