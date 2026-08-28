namespace EcommerceTxPr.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(
        string type,
        string payload,
        DateTime occurredOnUtc)
    {
        EnsureNotBlank(type, nameof(type));
        EnsureNotBlank(payload, nameof(payload));

        if (occurredOnUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Outbox occurrence time must be UTC.",
                nameof(occurredOnUtc));
        }

        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public void MarkProcessed(DateTime processedOnUtc)
    {
        if (processedOnUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Outbox processing time must be UTC.",
                nameof(processedOnUtc));
        }

        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        EnsureNotBlank(error, nameof(error));

        if (error.Length > MaxErrorLength)
        {
            throw new ArgumentException(
                $"Outbox error must not exceed {MaxErrorLength} characters.",
                nameof(error));
        }

        Error = error;
    }

    private static void EnsureNotBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value cannot be null or blank.",
                parameterName);
        }
    }
}
