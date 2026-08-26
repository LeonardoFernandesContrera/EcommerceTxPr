using EcommerceTxPr.Infrastructure.Outbox;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

namespace EcommerceTxPr.Infrastructure.RabbitMq;

internal sealed class RabbitMqOutboxMessagePublisher
    : IOutboxMessagePublisher, IAsyncDisposable
{
    private readonly IRabbitMqPublisherSessionFactory _sessionFactory;
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private IRabbitMqPublisherSession? _session;

    public RabbitMqOutboxMessagePublisher(
        IRabbitMqPublisherSessionFactory sessionFactory,
        IOptions<RabbitMqOptions> options)
    {
        _sessionFactory = sessionFactory;
        _options = options.Value;
    }

    public async Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            try
            {
                _session ??= await _sessionFactory
                    .CreateAsync(cancellationToken)
                    .ConfigureAwait(false);
                var publication = RabbitMqPublicationFactory.Create(
                    message,
                    _options);
                await _session
                    .PublishAsync(publication, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OutboxPublicationException)
            {
                await InvalidateSessionAsync().ConfigureAwait(false);
                throw;
            }
            catch (PublishException exception)
            {
                await InvalidateSessionAsync().ConfigureAwait(false);
                throw new OutboxPublicationException(
                    OutboxPublicationFailureCategory.ConfirmationOrRouting,
                    exception);
            }
            catch (Exception exception)
            {
                await InvalidateSessionAsync().ConfigureAwait(false);
                throw new OutboxPublicationException(
                    OutboxPublicationFailureCategory.Publish,
                    exception);
            }
        }
        finally
        {
            _publishGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publishGate.WaitAsync().ConfigureAwait(false);

        try
        {
            await InvalidateSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _publishGate.Release();
            _publishGate.Dispose();
        }
    }

    private async ValueTask InvalidateSessionAsync()
    {
        var session = _session;
        _session = null;

        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }
}
