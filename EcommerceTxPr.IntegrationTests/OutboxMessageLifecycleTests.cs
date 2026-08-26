using EcommerceTxPr.Infrastructure.Outbox;

namespace EcommerceTxPr.IntegrationTests;

public sealed class OutboxMessageLifecycleTests
{
    [Fact]
    public void MarkProcessed_sets_utc_timestamp_and_clears_previous_error()
    {
        var message = CreateMessage();
        var processedOnUtc = new DateTime(
            2026,
            8,
            25,
            14,
            30,
            0,
            DateTimeKind.Utc);
        message.RecordFailure("RabbitMQ publication failed (Connection).");

        message.MarkProcessed(processedOnUtc);

        Assert.Equal(processedOnUtc, message.ProcessedOnUtc);
        Assert.Null(message.Error);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void MarkProcessed_rejects_non_utc_timestamp(DateTimeKind kind)
    {
        var message = CreateMessage();
        var timestamp = DateTime.SpecifyKind(
            new DateTime(2026, 8, 25, 14, 30, 0),
            kind);

        Assert.Throws<ArgumentException>(() =>
            message.MarkProcessed(timestamp));
        Assert.Null(message.ProcessedOnUtc);
    }

    [Fact]
    public void RecordFailure_sets_error_and_keeps_message_pending()
    {
        var message = CreateMessage();

        message.RecordFailure("RabbitMQ publication failed (Topology).");

        Assert.Equal(
            "RabbitMQ publication failed (Topology).",
            message.Error);
        Assert.Null(message.ProcessedOnUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecordFailure_rejects_missing_or_blank_error(string? error)
    {
        var message = CreateMessage();

        Assert.Throws<ArgumentException>(() =>
            message.RecordFailure(error!));
        Assert.Null(message.Error);
    }

    [Fact]
    public void RecordFailure_rejects_error_over_database_limit()
    {
        var message = CreateMessage();

        Assert.Throws<ArgumentException>(() =>
            message.RecordFailure(new string('x', 2001)));
        Assert.Null(message.Error);
    }

    private static OutboxMessage CreateMessage()
    {
        return new OutboxMessage(
            OutboxMessageTypes.PaymentSucceededV1,
            "{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}",
            new DateTime(2026, 8, 25, 12, 30, 0, DateTimeKind.Utc));
    }
}
