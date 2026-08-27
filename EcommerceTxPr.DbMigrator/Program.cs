using EcommerceTxPr.Infrastructure;
using EcommerceTxPr.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString(
    "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Database migration failed: connection string " +
        "'DefaultConnection' is not configured.");
    return 1;
}

builder.Services.AddDatabase(connectionString);

using var host = builder.Build();

try
{
    await using var scope = host.Services.CreateAsyncScope();
    var context = scope.ServiceProvider
        .GetRequiredService<EcommerceTxPrDbContext>();

    await context.Database.MigrateAsync().ConfigureAwait(false);
    Console.WriteLine("Database migration completed successfully.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Database migration failed ({exception.GetType().Name}).");
    return 1;
}
