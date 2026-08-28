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
        var sessionCompletion = CreateSessionCompletion();

        await RabbitMqPaymentEventsConsumerSessionFactory.HandleDeliveryAsync(
            handler,
            CreateDelivery(),
            deliveryTag: 42,
            acknowledger,
            sessionCompletion,
            CancellationToken.None);

        var call = Assert.Single(acknowledger.Calls);
        Assert.Equal(AcknowledgementKind.Ack, call.Kind);
        Assert.Equal((ulong)42, call.DeliveryTag);
        Assert.False(call.Requeue);
        Assert.True(call.ScopeWasDisposed);
        Assert.Single(state.Deliveries);
        Assert.False(sessionCompletion.Task.IsCompleted);
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
        var sessionCompletion = CreateSessionCompletion();

        await RabbitMqPaymentEventsConsumerSessionFactory.HandleDeliveryAsync(
            CreateHandler(provider),
            CreateDelivery(),
            deliveryTag: 43,
            acknowledger,
            sessionCompletion,
            CancellationToken.None);

        var call = Assert.Single(acknowledger.Calls);
        Assert.Equal(AcknowledgementKind.Reject, call.Kind);
        Assert.Equal((ulong)43, call.DeliveryTag);
        Assert.False(call.Requeue);
        Assert.False(sessionCompletion.Task.IsCompleted);
    }

    [Fact]
    public async Task Transient_exception_nacks_then_ends_session()
    {
        var state = new ProcessorState
        {
            Exception = new InvalidOperationException(
                "temporary database failure")
        };
        await using var provider = CreateProvider(state);
        var sessionCompletion = CreateSessionCompletion();
        var acknowledger = new GatedNackAcknowledger(
            () => state.ScopeDisposed);

        var handling = RabbitMqPaymentEventsConsumerSessionFactory
            .HandleDeliveryAsync(
                CreateHandler(provider),
                CreateDelivery(),
                deliveryTag: 44,
                acknowledger,
                sessionCompletion,
                CancellationToken.None);

        await acknowledger.NackStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal((ulong)44, acknowledger.DeliveryTag);
        Assert.True(acknowledger.Requeue);
        Assert.True(acknowledger.ScopeWasDisposed);
        Assert.False(handling.IsCompleted);
        Assert.False(sessionCompletion.Task.IsCompleted);

        acknowledger.ReleaseNack();
        await handling;

        Assert.True(sessionCompletion.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Completed_session_does_not_process_another_delivery()
    {
        var state = new ProcessorState
        {
            Result = PaymentIntegrationEventProcessingResult.Processed
        };
        await using var provider = CreateProvider(state);
        var acknowledger = new RecordingPaymentDeliveryAcknowledger();
        var sessionCompletion = CreateSessionCompletion();
        sessionCompletion.SetResult(null);

        await RabbitMqPaymentEventsConsumerSessionFactory.HandleDeliveryAsync(
            CreateHandler(provider),
            CreateDelivery(),
            deliveryTag: 45,
            acknowledger,
            sessionCompletion,
            CancellationToken.None);

        Assert.Empty(state.Deliveries);
        Assert.Empty(acknowledger.Calls);
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

    private static TaskCompletionSource<object?> CreateSessionCompletion()
    {
        return new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
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

    private sealed class GatedNackAcknowledger
        : IPaymentDeliveryAcknowledger
    {
        private readonly Func<bool> _scopeDisposed;
        private readonly TaskCompletionSource<object?> _releaseNack = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedNackAcknowledger(Func<bool> scopeDisposed)
        {
            _scopeDisposed = scopeDisposed;
        }

        public TaskCompletionSource<object?> NackStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ulong DeliveryTag { get; private set; }

        public bool Requeue { get; private set; }

        public bool ScopeWasDisposed { get; private set; }

        public Task AckAsync(
            ulong deliveryTag,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("ACK was not expected.");
        }

        public Task RejectAsync(
            ulong deliveryTag,
            bool requeue,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Reject was not expected.");
        }

        public async Task NackAsync(
            ulong deliveryTag,
            bool requeue,
            CancellationToken cancellationToken)
        {
            DeliveryTag = deliveryTag;
            Requeue = requeue;
            ScopeWasDisposed = _scopeDisposed();
            NackStarted.TrySetResult(null);
            await _releaseNack.Task.WaitAsync(cancellationToken);
        }

        public void ReleaseNack()
        {
            _releaseNack.TrySetResult(null);
        }
    }
}
