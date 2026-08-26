using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class PaymentEventsConsumerBackgroundServiceTests
{
    [Fact]
    public async Task Failed_initial_session_can_be_retried_by_later_cycle()
    {
        var successfulSession = new RecordingConsumerSession(completed: true);
        var factory = new ScriptedConsumerSessionFactory();
        factory.EnqueueFailure(new InvalidOperationException("broker unavailable"));
        factory.EnqueueSession(successfulSession);
        var service = CreateService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunSessionCycleAsync(CancellationToken.None));
        await service.RunSessionCycleAsync(CancellationToken.None);

        Assert.Equal(2, factory.CreateCalls);
        Assert.True(successfulSession.IsDisposed);
    }

    [Fact]
    public async Task Healthy_session_is_reused_until_its_completion_signal()
    {
        var session = new RecordingConsumerSession(completed: false);
        var factory = new ScriptedConsumerSessionFactory();
        factory.EnqueueSession(session);
        var service = CreateService(factory);

        var cycle = service.RunSessionCycleAsync(CancellationToken.None);
        await Task.Yield();

        Assert.False(cycle.IsCompleted);
        Assert.Equal(1, factory.CreateCalls);
        Assert.False(session.IsDisposed);

        session.Complete();
        await cycle;

        Assert.True(session.IsDisposed);
        Assert.Equal(1, factory.CreateCalls);
    }

    private static PaymentEventsConsumerBackgroundService CreateService(
        IRabbitMqPaymentEventsConsumerSessionFactory factory)
    {
        return new PaymentEventsConsumerBackgroundService(
            factory,
            Options.Create(new RabbitMqOptions
            {
                Enabled = true,
                ConsumerReconnectDelaySeconds = 5
            }),
            NullLogger<PaymentEventsConsumerBackgroundService>.Instance);
    }

    private sealed class ScriptedConsumerSessionFactory
        : IRabbitMqPaymentEventsConsumerSessionFactory
    {
        private readonly Queue<object> _results = new();

        public int CreateCalls { get; private set; }

        public void EnqueueFailure(Exception exception)
        {
            _results.Enqueue(exception);
        }

        public void EnqueueSession(IRabbitMqPaymentEventsConsumerSession session)
        {
            _results.Enqueue(session);
        }

        public Task<IRabbitMqPaymentEventsConsumerSession> CreateAsync(
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            var result = _results.Dequeue();

            return result switch
            {
                IRabbitMqPaymentEventsConsumerSession session =>
                    Task.FromResult(session),
                Exception exception =>
                    Task.FromException<IRabbitMqPaymentEventsConsumerSession>(
                        exception),
                _ => throw new InvalidOperationException(
                    "Unsupported scripted consumer session result.")
            };
        }
    }

    private sealed class RecordingConsumerSession
        : IRabbitMqPaymentEventsConsumerSession
    {
        private readonly TaskCompletionSource<object?> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingConsumerSession(bool completed)
        {
            if (completed)
            {
                _completion.SetResult(null);
            }
        }

        public Task Completion => _completion.Task;

        public bool IsDisposed { get; private set; }

        public void Complete()
        {
            _completion.TrySetResult(null);
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
