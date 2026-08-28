using EcommerceTxPr.Infrastructure.Outbox;
using EcommerceTxPr.Infrastructure.RabbitMq;
using Microsoft.Extensions.Options;

namespace EcommerceTxPr.IntegrationTests;

public sealed class RabbitMqOutboxMessagePublisherTests
{
    [Fact]
    public async Task Construction_is_lazy_and_successful_session_is_reused()
    {
        var session = new RecordingSession();
        var sessionFactory = new ScriptedSessionFactory();
        sessionFactory.EnqueueSession(session);
        await using var publisher = CreatePublisher(sessionFactory);
        var first = CreateMessage(OutboxMessageTypes.PaymentSucceededV1);
        var second = CreateMessage(OutboxMessageTypes.PaymentFailedV1);

        Assert.Equal(0, sessionFactory.CreateCalls);

        await publisher.PublishAsync(first, CancellationToken.None);
        await publisher.PublishAsync(second, CancellationToken.None);

        Assert.Equal(1, sessionFactory.CreateCalls);
        Assert.Collection(
            session.Publications,
            publication => Assert.Equal(
                first.Id.ToString("D"),
                publication.Properties.MessageId),
            publication => Assert.Equal(
                second.Id.ToString("D"),
                publication.Properties.MessageId));
    }

    [Fact]
    public async Task Initial_connection_failure_is_retried_only_on_later_publish_call()
    {
        var connectionFailure = new OutboxPublicationException(
            OutboxPublicationFailureCategory.Connection,
            new InvalidOperationException("broker unavailable"));
        var successfulSession = new RecordingSession();
        var sessionFactory = new ScriptedSessionFactory();
        sessionFactory.EnqueueFailure(connectionFailure);
        sessionFactory.EnqueueSession(successfulSession);
        await using var publisher = CreatePublisher(sessionFactory);
        var message = CreateMessage(OutboxMessageTypes.PaymentSucceededV1);

        var thrown = await Assert.ThrowsAsync<OutboxPublicationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));
        Assert.Equal(
            OutboxPublicationFailureCategory.Connection,
            thrown.Category);
        Assert.Equal(1, sessionFactory.CreateCalls);

        await publisher.PublishAsync(message, CancellationToken.None);

        Assert.Equal(2, sessionFactory.CreateCalls);
        var publication = Assert.Single(successfulSession.Publications);
        Assert.Equal(message.Id.ToString("D"), publication.Properties.MessageId);
    }

    [Fact]
    public async Task Failed_session_is_disposed_and_next_call_uses_new_session()
    {
        var failedSession = new RecordingSession
        {
            Failure = new OutboxPublicationException(
                OutboxPublicationFailureCategory.ConfirmationOrRouting,
                new InvalidOperationException("nack or return"))
        };
        var successfulSession = new RecordingSession();
        var sessionFactory = new ScriptedSessionFactory();
        sessionFactory.EnqueueSession(failedSession);
        sessionFactory.EnqueueSession(successfulSession);
        await using var publisher = CreatePublisher(sessionFactory);
        var message = CreateMessage(OutboxMessageTypes.PaymentFailedV1);

        await Assert.ThrowsAsync<OutboxPublicationException>(
            () => publisher.PublishAsync(message, CancellationToken.None));
        await publisher.PublishAsync(message, CancellationToken.None);

        Assert.True(failedSession.IsDisposed);
        Assert.Equal(2, sessionFactory.CreateCalls);
        Assert.Equal(
            message.Id.ToString("D"),
            Assert.Single(failedSession.Publications).Properties.MessageId);
        Assert.Equal(
            message.Id.ToString("D"),
            Assert.Single(successfulSession.Publications).Properties.MessageId);
    }

    private static RabbitMqOutboxMessagePublisher CreatePublisher(
        IRabbitMqPublisherSessionFactory sessionFactory)
    {
        return new RabbitMqOutboxMessagePublisher(
            sessionFactory,
            Options.Create(new RabbitMqOptions
            {
                Enabled = true,
                ExchangeName = "ecommerce.events"
            }));
    }

    private static OutboxMessage CreateMessage(string type)
    {
        return new OutboxMessage(
            type,
            "{\"value\":1}",
            new DateTime(2026, 8, 25, 12, 30, 0, DateTimeKind.Utc));
    }

    private sealed class ScriptedSessionFactory
        : IRabbitMqPublisherSessionFactory
    {
        private readonly Queue<object> _results = new();

        public int CreateCalls { get; private set; }

        public void EnqueueSession(IRabbitMqPublisherSession session)
        {
            _results.Enqueue(session);
        }

        public void EnqueueFailure(OutboxPublicationException exception)
        {
            _results.Enqueue(exception);
        }

        public Task<IRabbitMqPublisherSession> CreateAsync(
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            var result = _results.Dequeue();

            return result switch
            {
                IRabbitMqPublisherSession session => Task.FromResult(session),
                OutboxPublicationException exception =>
                    Task.FromException<IRabbitMqPublisherSession>(exception),
                _ => throw new InvalidOperationException("Unsupported test result.")
            };
        }
    }

    private sealed class RecordingSession : IRabbitMqPublisherSession
    {
        public List<RabbitMqPublication> Publications { get; } = new();

        public OutboxPublicationException? Failure { get; init; }

        public bool IsDisposed { get; private set; }

        public Task PublishAsync(
            RabbitMqPublication publication,
            CancellationToken cancellationToken)
        {
            Publications.Add(publication);

            return Failure is null
                ? Task.CompletedTask
                : Task.FromException(Failure);
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
