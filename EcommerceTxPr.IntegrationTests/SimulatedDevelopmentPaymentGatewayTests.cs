using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.Infrastructure;
using EcommerceTxPr.Infrastructure.Payments;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class SimulatedDevelopmentPaymentGatewayTests
{
    private static readonly Guid PaymentId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Succeeded_configuration_returns_deterministic_success()
    {
        using var provider = CreateProvider("Succeeded");
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(PaymentId, 25m),
            CancellationToken.None);

        Assert.Equal(PaymentGatewayStatus.Succeeded, result.Status);
        Assert.Equal(
            "simulated-11111111111111111111111111111111",
            result.ProviderReference);
        Assert.Null(result.FailureCode);
    }

    [Fact]
    public async Task Failed_configuration_returns_deterministic_failure()
    {
        using var provider = CreateProvider("Failed");
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(PaymentId, 25m),
            CancellationToken.None);

        Assert.Equal(PaymentGatewayStatus.Failed, result.Status);
        Assert.Equal("SimulatedDecline", result.FailureCode);
        Assert.Null(result.ProviderReference);
    }

    [Fact]
    public async Task Missing_configuration_defaults_to_succeeded()
    {
        using var provider = CreateProvider(null);
        var gateway = provider.GetRequiredService<IPaymentGateway>();

        var result = await gateway.ProcessAsync(
            new PaymentGatewayRequest(PaymentId, 25m),
            CancellationToken.None);

        Assert.Equal(PaymentGatewayStatus.Succeeded, result.Status);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("succeeded")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("999")]
    [InlineData(" Succeeded")]
    [InlineData("Succeeded ")]
    public async Task Unsupported_configuration_fails_host_startup(string outcome)
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["PaymentGateway:SimulatedOutcome"] = outcome
                    }))
            .ConfigureServices((context, services) =>
                services.AddSimulatedDevelopmentPaymentGateway(
                    context.Configuration))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());
    }

    [Fact]
    public void Integration_factory_replaces_gateway_with_explicit_test_double()
    {
        using var factory = new CustomerApiFactory();
        using var client = factory.CreateClientWithDatabase();
        using var scope = factory.Services.CreateScope();

        var gateway = scope.ServiceProvider.GetRequiredService<IPaymentGateway>();

        Assert.IsType<DeterministicTestPaymentGateway>(gateway);
        Assert.IsNotType<SimulatedDevelopmentPaymentGateway>(gateway);
    }

    private static ServiceProvider CreateProvider(string? outcome)
    {
        var values = outcome is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>
            {
                ["PaymentGateway:SimulatedOutcome"] = outcome
            };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSimulatedDevelopmentPaymentGateway(configuration);
        return services.BuildServiceProvider();
    }
}
