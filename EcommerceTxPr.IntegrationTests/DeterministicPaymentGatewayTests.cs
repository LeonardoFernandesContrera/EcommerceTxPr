using EcommerceTxPr.Application.Payments.Gateways;
using EcommerceTxPr.IntegrationTests.Infrastructure;

namespace EcommerceTxPr.IntegrationTests;

public sealed class DeterministicPaymentGatewayTests
{
    private static readonly Guid PaymentId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Equivalent_request_replays_exact_result_without_second_effect()
    {
        var gateway = new DeterministicTestPaymentGateway
        {
            Result = PaymentGatewayResult.Succeeded("provider-reference-X")
        };
        var request = new PaymentGatewayRequest(
            PaymentId,
            25m,
            "payment-11111111111111111111111111111111");

        var first = await gateway.ProcessAsync(request, CancellationToken.None);
        gateway.Result = PaymentGatewayResult.Failed("later-configuration");
        var second = await gateway.ProcessAsync(request, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(PaymentGatewayStatus.Succeeded, second.Status);
        Assert.Equal("provider-reference-X", second.ProviderReference);
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
    }

    [Fact]
    public async Task Lost_response_observation_replays_stored_success_on_retry()
    {
        var gateway = new DeterministicTestPaymentGateway
        {
            Result = PaymentGatewayResult.Succeeded("stored-success")
        };
        gateway.EnqueueObservation(PaymentGatewayResult.Indeterminate());
        var request = new PaymentGatewayRequest(
            PaymentId,
            25m,
            "payment-11111111111111111111111111111111");

        var first = await gateway.ProcessAsync(request, CancellationToken.None);
        var second = await gateway.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(PaymentGatewayStatus.Indeterminate, first.Status);
        Assert.Equal(PaymentGatewayStatus.Succeeded, second.Status);
        Assert.Equal("stored-success", second.ProviderReference);
        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_key_with_different_request_fails_closed(
        bool changePaymentId)
    {
        var gateway = new DeterministicTestPaymentGateway();
        const string key = "payment-shared-provider-key";
        await gateway.ProcessAsync(
            new PaymentGatewayRequest(PaymentId, 25m, key),
            CancellationToken.None);
        var conflictingRequest = changePaymentId
            ? new PaymentGatewayRequest(Guid.NewGuid(), 25m, key)
            : new PaymentGatewayRequest(PaymentId, 26m, key);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gateway.ProcessAsync(
                conflictingRequest,
                CancellationToken.None));

        Assert.Equal(2, gateway.GatewayRequestCount);
        Assert.Equal(1, gateway.ExternalEffectExecutionCount);
    }
}
