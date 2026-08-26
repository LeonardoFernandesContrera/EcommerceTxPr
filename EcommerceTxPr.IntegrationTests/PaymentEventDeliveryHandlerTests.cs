using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using EcommerceTxPr.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentEventDeliveryHandlerTests
{
    [Theory]
    [InlineData(PaymentIntegrationEventProcessingResult.Processed)]
    [InlineData(PaymentIntegrationEventProcessingResult.Duplicate)]
    public async Task Durable_result_acks_one_delivery_after_scope_disposal(
        PaymentIntegrationEventProcessingResult result)
    {
        var state = new ProcessorState { Result = result };
        await using var provider = CreateProvider(state);
        var handler = CreateHandler(provider);
        var acknowledger = new RecordingPaymentDeliveryAcknowledger(
            () => state.ScopeDisposed);

        await handler.HandleAsync(
            CreateDelivery(),
            deliveryTag: 42,
            acknowledger,
            CancellationToken.None);

        var call = Assert.Single(acknowledger.Calls);
        Assert.Equal(AcknowledgementKind.Ack, call.Kind);
        Assert.Equal((ulong)42, call.DeliveryTag);
        Assert.False(call.Requeue);
        Assert.True(call.ScopeWasDisposed);
        Assert.Single(state.Deliveries);
    }

    [Fact]
    public async Task Poison_result_rejects_without_requeue()
    {
        var state = new ProcessorState
        {
            Result = PaymentIntegrationEventProcessingResult.Poison
        };
        await using var provider = CreateProvider(state);
        var acknowledger = new RecordingPaymentDeliveryAcknowledger();

        await CreateHandler(provider).HandleAsync(
            CreateDelivery(),
            deliveryTag: 43,
            acknowledger,
            CancellationToken.None);

        var call = Assert.Single(acknowledger.Calls);
        Assert.Equal(AcknowledgementKind.Reject, call.Kind);
        Assert.Equal((ulong)43, call.DeliveryTag);
        Assert.False(call.Requeue);
    }

    [Fact]
    public async Task Transient_processor_exception_nacks_with_requeue()
    {
        var state = new ProcessorState
        {
            Exception = new InvalidOperationException(
                "temporary database failure")
        };
        await using var provider = CreateProvider(state);
        var acknowledger = new RecordingPaymentDeliveryAcknowledger(
            () => state.ScopeDisposed);

        await CreateHandler(provider).HandleAsync(
            CreateDelivery(),
            deliveryTag: 44,
            acknowledger,
            CancellationToken.None);

        var call = Assert.Single(acknowledger.Calls);
        Assert.Equal(AcknowledgementKind.Nack, call.Kind);
        Assert.Equal((ulong)44, call.DeliveryTag);
        Assert.True(call.Requeue);
        Assert.True(call.ScopeWasDisposed);
    }

    private static ServiceProvider CreateProvider(ProcessorState state)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddScoped<
            IPaymentIntegrationEventProcessor,
            ScriptedProcessor>();
        return services.BuildServiceProvider();
    }

    private static PaymentEventDeliveryHandler CreateHandler(
        IServiceProvider serviceProvider)
    {
        return new PaymentEventDeliveryHandler(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PaymentEventDeliveryHandler>.Instance);
    }

    private static PaymentIntegrationEventDelivery CreateDelivery()
    {
        return new PaymentIntegrationEventDelivery(
            "11111111-1111-1111-1111-111111111111",
            OutboxMessageTypes.PaymentSucceededV1,
            OutboxMessageTypes.PaymentSucceededV1,
            "{}"u8.ToArray());
    }

    private sealed class ProcessorState
    {
        public PaymentIntegrationEventProcessingResult Result { get; init; }

        public Exception? Exception { get; init; }

        public bool ScopeDisposed { get; set; }

        public List<PaymentIntegrationEventDelivery> Deliveries { get; } = new();
    }

    private sealed class ScriptedProcessor
        : IPaymentIntegrationEventProcessor, IAsyncDisposable
    {
        private readonly ProcessorState _state;

        public ScriptedProcessor(ProcessorState state)
        {
            _state = state;
        }

        public Task<PaymentIntegrationEventProcessingResult> ProcessAsync(
            PaymentIntegrationEventDelivery delivery,
            CancellationToken cancellationToken)
        {
            _state.Deliveries.Add(delivery);

            return _state.Exception is null
                ? Task.FromResult(_state.Result)
                : Task.FromException<PaymentIntegrationEventProcessingResult>(
                    _state.Exception);
        }

        public ValueTask DisposeAsync()
        {
            _state.ScopeDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
