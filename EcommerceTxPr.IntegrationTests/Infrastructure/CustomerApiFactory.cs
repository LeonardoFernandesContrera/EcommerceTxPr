using System.Data.Common;
using EcommerceTxPr.Application.Payments.Gateways;
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
    private readonly IReadOnlyDictionary<string, string?>? _configurationValues;
    private readonly string _databaseConnectionString =
        new SqliteConnectionStringBuilder
        {
            DataSource = $"EcommerceTxPrTests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

    static CustomerApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            TestConnectionString);
    }

    public CustomerApiFactory(
        Action<IServiceCollection>? configureTestServices = null,
        IReadOnlyDictionary<string, string?>? configurationValues = null)
    {
        _configureTestServices = configureTestServices;
        _configurationValues = configurationValues;
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

            if (_configurationValues is not null)
            {
                configuration.AddInMemoryCollection(_configurationValues);
            }
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<EcommerceTxPrDbContext>();
            services.RemoveAll<DbContextOptions<EcommerceTxPrDbContext>>();

            services.AddSingleton<DbConnection>(_ =>
            {
                var connection = new SqliteConnection(
                    _databaseConnectionString);
                connection.Open();
                return connection;
            });

            services.AddDbContext<EcommerceTxPrDbContext>(options =>
                options.UseSqlite(_databaseConnectionString));

            services.RemoveAll<IDatabaseErrorClassifier>();
            services.AddScoped<
                IDatabaseErrorClassifier,
                SqliteDatabaseErrorClassifier>();

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<DeterministicTestPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(serviceProvider =>
                serviceProvider.GetRequiredService<DeterministicTestPaymentGateway>());

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

        _ = Services.GetRequiredService<DbConnection>();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EcommerceTxPrDbContext>();
        context.Database.EnsureCreated();

        return client;
    }
}
