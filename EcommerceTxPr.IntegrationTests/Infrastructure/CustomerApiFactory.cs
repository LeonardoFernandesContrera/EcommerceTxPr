using System.Data.Common;
using EcommerceTxPr.Infrastructure.Context;
using EcommerceTxPr.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        "Server=test-only;Database=test-only;User Id=test-only;Password=test-only";

    private readonly Action<IServiceCollection>? _configureTestServices;

    static CustomerApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            TestConnectionString);
    }

    public CustomerApiFactory(
        Action<IServiceCollection>? configureTestServices = null)
    {
        _configureTestServices = configureTestServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = TestConnectionString
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<EcommerceTxPrDbContext>();
            services.RemoveAll<DbContextOptions<EcommerceTxPrDbContext>>();

            services.AddSingleton<DbConnection>(_ =>
            {
                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                return connection;
            });

            services.AddDbContext<EcommerceTxPrDbContext>((serviceProvider, options) =>
            {
                var connection = serviceProvider.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);
            });

            services.RemoveAll<IDatabaseErrorClassifier>();
            services.AddScoped<
                IDatabaseErrorClassifier,
                SqliteDatabaseErrorClassifier>();

            _configureTestServices?.Invoke(services);
        });
    }

    public HttpClient CreateClientWithDatabase()
    {
        var client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EcommerceTxPrDbContext>();
        context.Database.EnsureCreated();

        return client;
    }
}
