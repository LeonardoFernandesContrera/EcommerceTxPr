using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class PaymentEventsConsumerBackgroundService
    : BackgroundService
{
    private readonly IRabbitMqPaymentEventsConsumerSessionFactory
        _sessionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<PaymentEventsConsumerBackgroundService> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public PaymentEventsConsumerBackgroundService(
        IRabbitMqPaymentEventsConsumerSessionFactory sessionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<PaymentEventsConsumerBackgroundService> logger)
        : this(
            sessionFactory,
            options,
            logger,
            static (delay, cancellationToken) =>
                Task.Delay(delay, cancellationToken))
    {
    }

    internal PaymentEventsConsumerBackgroundService(
        IRabbitMqPaymentEventsConsumerSessionFactory sessionFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<PaymentEventsConsumerBackgroundService> logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _sessionFactory = sessionFactory;
        _options = options.Value;
        _logger = logger;
        _delayAsync = delayAsync;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSessionCycleAsync(stoppingToken)
                    .ConfigureAwait(false);
                _logger.LogWarning(
                    "RabbitMQ payment-event consumer session ended; a new "
                    + "session will be attempted after the reconnect delay.");
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "RabbitMQ payment-event consumer connection or session "
                    + "failed; a later cycle will retry.");
            }

            await _delayAsync(
                    TimeSpan.FromSeconds(
                        _options.ConsumerReconnectDelaySeconds),
                    stoppingToken)
                .ConfigureAwait(false);
        }
    }

    internal async Task RunSessionCycleAsync(
        CancellationToken cancellationToken)
    {
        await using var session = await _sessionFactory
            .CreateAsync(cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "RabbitMQ payment-event consumer session established.");
        await session.Completion
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
