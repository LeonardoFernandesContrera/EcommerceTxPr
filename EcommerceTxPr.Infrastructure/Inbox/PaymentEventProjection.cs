namespace EcommerceTxPr.Infrastructure.Inbox;

public sealed class PaymentEventProjection
{
    internal const int MaxProviderReferenceLength = 200;
    internal const int MaxFailureCodeLength = 100;

    private PaymentEventProjection()
    {
    }

    private PaymentEventProjection(
        Guid messageId,
        Guid paymentId,
        Guid orderId,
        decimal amount,
        PaymentEventOutcome outcome,
        DateTime occurredOnUtc,
        DateTime processedOnUtc,
        string? providerReference,
        string? failureCode)
    {
        EnsureNotEmpty(messageId, nameof(messageId));
        EnsureNotEmpty(paymentId, nameof(paymentId));
        EnsureNotEmpty(orderId, nameof(orderId));

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be greater than zero.");
        }

        EnsureUtc(occurredOnUtc, nameof(occurredOnUtc));
        EnsureUtc(processedOnUtc, nameof(processedOnUtc));

        MessageId = messageId;
        PaymentId = paymentId;
        OrderId = orderId;
        Amount = amount;
        Outcome = outcome;
        OccurredOnUtc = occurredOnUtc;
        ProcessedOnUtc = processedOnUtc;
        ProviderReference = providerReference;
        FailureCode = failureCode;
    }

    public Guid MessageId { get; private set; }

    public Guid PaymentId { get; private set; }

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentEventOutcome Outcome { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime ProcessedOnUtc { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    public static PaymentEventProjection Succeeded(
        Guid messageId,
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string providerReference,
        DateTime occurredOnUtc,
        DateTime processedOnUtc)
    {
        EnsureRequiredText(
            providerReference,
            MaxProviderReferenceLength,
            nameof(providerReference));

        return new PaymentEventProjection(
            messageId,
            paymentId,
            orderId,
            amount,
            PaymentEventOutcome.Succeeded,
            occurredOnUtc,
            processedOnUtc,
            providerReference,
            null);
    }

    public static PaymentEventProjection Failed(
        Guid messageId,
        Guid paymentId,
        Guid orderId,
        decimal amount,
        string failureCode,
        DateTime occurredOnUtc,
        DateTime processedOnUtc)
    {
        EnsureRequiredText(
            failureCode,
            MaxFailureCodeLength,
            nameof(failureCode));

        return new PaymentEventProjection(
            messageId,
            paymentId,
            orderId,
            amount,
            PaymentEventOutcome.Failed,
            occurredOnUtc,
            processedOnUtc,
            null,
            failureCode);
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier must be supplied.",
                parameterName);
        }
    }

    private static void EnsureRequiredText(
        string value,
        int maxLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Value must be supplied.",
                parameterName);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value must not exceed {maxLength} characters.",
                parameterName);
        }
    }

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
