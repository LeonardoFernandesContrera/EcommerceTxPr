namespace EcommerceTxPr.Infrastructure.Inbox;

public sealed class InboxMessage
{
    private InboxMessage()
    {
        Type = string.Empty;
    }

    public InboxMessage(
        Guid messageId,
        string type,
        DateTime processedOnUtc)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Message id must be supplied.",
                nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException(
                "Message type must be supplied.",
                nameof(type));
        }

        EnsureUtc(processedOnUtc, nameof(processedOnUtc));

        MessageId = messageId;
        Type = type;
        ProcessedOnUtc = processedOnUtc;
    }

    public Guid MessageId { get; private set; }

    public string Type { get; private set; }

    public DateTime ProcessedOnUtc { get; private set; }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Timestamp must be UTC.",
                parameterName);
        }
    }
}
