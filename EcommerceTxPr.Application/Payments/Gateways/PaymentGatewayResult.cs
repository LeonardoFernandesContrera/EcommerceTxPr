namespace EcommerceTxPr.Application.Payments.Gateways;

public sealed record PaymentGatewayResult
{
    private PaymentGatewayResult(
        PaymentGatewayStatus status,
        string? providerReference,
        string? failureCode)
    {
        Status = status;
        ProviderReference = providerReference;
        FailureCode = failureCode;
    }

    public PaymentGatewayStatus Status { get; }

    public string? ProviderReference { get; }

    public string? FailureCode { get; }

    public static PaymentGatewayResult Succeeded(string providerReference)
    {
        EnsureNotBlank(providerReference, nameof(providerReference));

        return new PaymentGatewayResult(
            PaymentGatewayStatus.Succeeded,
            providerReference,
            null);
    }

    public static PaymentGatewayResult Failed(string failureCode)
    {
        EnsureNotBlank(failureCode, nameof(failureCode));

        return new PaymentGatewayResult(
            PaymentGatewayStatus.Failed,
            null,
            failureCode);
    }

    public static PaymentGatewayResult Indeterminate()
    {
        return new PaymentGatewayResult(
            PaymentGatewayStatus.Indeterminate,
            null,
            null);
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
