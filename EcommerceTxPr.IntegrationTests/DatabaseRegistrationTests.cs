using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Repositories;
using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Application.Payments.Services;
using EcommerceTxPr.Infrastructure;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EcommerceTxPr.IntegrationTests;

public sealed class DatabaseRegistrationTests
{
    [Fact]
    public void AddDatabase_registers_only_the_dbcontext_path()
    {
        var services = new ServiceCollection();

        services.AddDatabase(
            "Server=localhost;Database=registration-test;" +
            "Integrated Security=True;TrustServerCertificate=True");

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType
                == typeof(EcommerceTxPrDbContext));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IPaymentGateway));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IPaymentService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOrderRepository));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
    }
}
