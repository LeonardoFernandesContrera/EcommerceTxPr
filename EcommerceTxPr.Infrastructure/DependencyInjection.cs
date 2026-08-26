using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Customers.Repositories;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Application.Payments.Repositories;
using EcommerceTxPr.Application.Products.Repositories;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.Persistence;
using EcommerceTxPr.Infrastructure.Payments;
using EcommerceTxPr.Infrastructure.RabbitMq;
using EcommerceTxPr.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<
                IOrderIdempotencyRepository,
                OrderIdempotencyRepository>();
            services.AddScoped<
                IDatabaseErrorClassifier,
                SqlServerDatabaseErrorClassifier>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }

        public static IServiceCollection AddSimulatedDevelopmentPaymentGateway(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddOptions<SimulatedDevelopmentPaymentGatewayOptions>()
                .Bind(configuration.GetSection(
                    SimulatedDevelopmentPaymentGatewayOptions.SectionName))
                .Validate(
                    options => options.TryGetOutcome(out _),
                    "PaymentGateway:SimulatedOutcome must be Succeeded or Failed.")
                .ValidateOnStart();
            services.AddScoped<
                IPaymentGateway,
                SimulatedDevelopmentPaymentGateway>();

            return services;
        }

        public static IServiceCollection AddRabbitMqOutboxDispatcher(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection(RabbitMqOptions.SectionName);

            services
                .AddOptions<RabbitMqOptions>()
                .Bind(section)
                .ValidateOnStart();
            services.AddSingleton<
                IValidateOptions<RabbitMqOptions>,
                RabbitMqOptionsValidator>();

            if (!section.GetValue<bool>(nameof(RabbitMqOptions.Enabled)))
            {
                return services;
            }

            services.AddSingleton<
                IRabbitMqPublisherSessionFactory,
                RabbitMqPublisherSessionFactory>();
            services.AddSingleton<
                IOutboxMessagePublisher,
                RabbitMqOutboxMessagePublisher>();
            services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
            services.AddHostedService<OutboxDispatcherBackgroundService>();
            services.AddScoped<
                IPaymentIntegrationEventProcessor,
                PaymentIntegrationEventProcessor>();
            services.AddSingleton<
                IPaymentEventDeliveryHandler,
                PaymentEventDeliveryHandler>();
            services.AddSingleton<
                IRabbitMqPaymentEventsConsumerSessionFactory,
                RabbitMqPaymentEventsConsumerSessionFactory>();
            services.AddHostedService<PaymentEventsConsumerBackgroundService>();

            return services;
        }
    }
}
