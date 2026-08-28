using EcommerceApi.V2.ErrorHandling;
using EcommerceApi.V2.Health;
using EcommerceTxPr.Application;
using EcommerceTxPr.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddRabbitMqOutboxDispatcher(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSimulatedDevelopmentPaymentGateway(
        builder.Configuration);
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Application is running."),
        tags: ["live"])
    .AddCheck<PrimaryDatabaseHealthCheck>(
        "sql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<RabbitMqDependencyHealthCheck>(
        "rabbitmq",
        failureStatus: HealthStatus.Degraded,
        tags: ["broker"],
        timeout: TimeSpan.FromSeconds(3));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks(
    "/health/live",
    CreateHealthCheckOptions(registration =>
        registration.Tags.Contains("live")));
app.MapHealthChecks(
    "/health/ready",
    CreateHealthCheckOptions(registration =>
        registration.Tags.Contains("ready")));
app.MapHealthChecks("/health", CreateHealthCheckOptions());

static HealthCheckOptions CreateHealthCheckOptions(
    Func<HealthCheckRegistration, bool>? predicate = null)
{
    return new HealthCheckOptions
    {
        Predicate = predicate,
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    };
}

app.Run();

public partial class Program
{
}
