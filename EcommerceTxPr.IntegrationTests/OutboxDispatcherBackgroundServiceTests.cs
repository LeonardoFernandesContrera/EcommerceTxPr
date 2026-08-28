using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxDispatcherBackgroundServiceTests
{
    [Fact]
    public async Task Every_iteration_resolves_a_new_scoped_dispatcher()
    {
        var recorder = new ScopeRecorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddScoped<IOutboxDispatcher, RecordingDispatcher>();
        await using var provider = services.BuildServiceProvider();
        var service = CreateService(provider.GetRequiredService<IServiceScopeFactory>());

        await service.DispatchOnceAsync(CancellationToken.None);
        await service.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(2, recorder.InstanceIds.Count);
        Assert.Equal(2, recorder.InstanceIds.Distinct().Count());
    }

    [Fact]
    public async Task Host_cancellation_is_propagated_from_iteration()
    {
        var services = new ServiceCollection();
        services.AddScoped<IOutboxDispatcher, CancellationDispatcher>();
        await using var provider = services.BuildServiceProvider();
        var service = CreateService(provider.GetRequiredService<IServiceScopeFactory>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DispatchOnceAsync(cancellation.Token));
    }

    private static OutboxDispatcherBackgroundService CreateService(
        IServiceScopeFactory scopeFactory)
    {
        return new OutboxDispatcherBackgroundService(
            scopeFactory,
            Options.Create(new RabbitMqOptions
            {
                Enabled = true,
                PollingIntervalSeconds = 5
            }),
            NullLogger<OutboxDispatcherBackgroundService>.Instance);
    }

    private sealed class ScopeRecorder
    {
        public List<Guid> InstanceIds { get; } = new();
    }

    private sealed class RecordingDispatcher : IOutboxDispatcher
    {
        private readonly ScopeRecorder _recorder;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingDispatcher(ScopeRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task<OutboxDispatchResult> DispatchBatchAsync(
            CancellationToken cancellationToken)
        {
            _recorder.InstanceIds.Add(_instanceId);
            return Task.FromResult(new OutboxDispatchResult(0, 0, 0));
        }
    }

    private sealed class CancellationDispatcher : IOutboxDispatcher
    {
        public Task<OutboxDispatchResult> DispatchBatchAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromCanceled<OutboxDispatchResult>(cancellationToken);
        }
    }
}
