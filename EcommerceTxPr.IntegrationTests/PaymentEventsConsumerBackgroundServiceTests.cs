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

    [Fact]
    public async Task Ended_session_is_disposed_and_reconnect_delay_throttles_next_session()
    {
        var firstSession = new RecordingConsumerSession(completed: true);
        var secondSession = new RecordingConsumerSession(completed: false);
        var factory = new ScriptedConsumerSessionFactory();
        factory.EnqueueSession(firstSession);
        factory.EnqueueSession(secondSession);
        var delayStarted = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = CreateService(
            factory,
            (delay, cancellationToken) =>
            {
                delayStarted.TrySetResult(delay);
                return releaseDelay.Task.WaitAsync(cancellationToken);
            });

        await service.StartAsync(CancellationToken.None);
        var observedDelay = await delayStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(5), observedDelay);
        Assert.True(firstSession.IsDisposed);
        Assert.Equal(1, factory.CreateCalls);

        releaseDelay.SetResult(null);
        await factory.SecondCreateStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(2, factory.CreateCalls);
        Assert.False(secondSession.IsDisposed);

        await service.StopAsync(CancellationToken.None);

        Assert.True(secondSession.IsDisposed);
    }

    private static PaymentEventsConsumerBackgroundService CreateService(
        IRabbitMqPaymentEventsConsumerSessionFactory factory,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        var options = Options.Create(new RabbitMqOptions
        {
            Enabled = true,
            ConsumerReconnectDelaySeconds = 5
        });
        var logger = NullLogger<PaymentEventsConsumerBackgroundService>.Instance;

        return delayAsync is null
            ? new PaymentEventsConsumerBackgroundService(
                factory,
                options,
                logger)
            : new PaymentEventsConsumerBackgroundService(
                factory,
                options,
                logger,
                delayAsync);
    }

    private sealed class ScriptedConsumerSessionFactory
        : IRabbitMqPaymentEventsConsumerSessionFactory
    {
        private readonly Queue<object> _results = new();

        public TaskCompletionSource<object?> SecondCreateStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

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
            if (CreateCalls == 2)
            {
                SecondCreateStarted.TrySetResult(null);
            }

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
