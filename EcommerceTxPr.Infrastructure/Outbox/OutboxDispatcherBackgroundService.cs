using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.Infrastructure.Outbox;

internal sealed class OutboxDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;

    public OutboxDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await DispatchOnceAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (result.LoadedCount > 0)
                {
                    _logger.LogInformation(
                        "Outbox batch loaded {LoadedCount} messages, published "
                        + "{PublishedCount}, and failed {FailedCount}.",
                        result.LoadedCount,
                        result.PublishedCount,
                        result.FailedCount);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Outbox dispatcher cycle failed before lifecycle state "
                    + "could be persisted.");
            }

            await Task.Delay(
                    TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                    stoppingToken)
                .ConfigureAwait(false);
        }
    }

    internal async Task<OutboxDispatchResult> DispatchOnceAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider
            .GetRequiredService<IOutboxDispatcher>();
        return await dispatcher
            .DispatchBatchAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
