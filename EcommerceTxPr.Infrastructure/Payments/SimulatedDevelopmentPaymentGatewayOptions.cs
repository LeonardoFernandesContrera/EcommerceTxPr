namespace EcommerceTxPr.Infrastructure.Payments;

public sealed class SimulatedDevelopmentPaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string SimulatedOutcome { get; set; } =
        nameof(SimulatedPaymentOutcome.Succeeded);

    internal bool TryGetOutcome(out SimulatedPaymentOutcome outcome)
    {
        if (string.Equals(
                SimulatedOutcome,
                nameof(SimulatedPaymentOutcome.Succeeded),
                StringComparison.Ordinal))
        {
            outcome = SimulatedPaymentOutcome.Succeeded;
            return true;
        }

        if (string.Equals(
                SimulatedOutcome,
                nameof(SimulatedPaymentOutcome.Failed),
                StringComparison.Ordinal))
        {
            outcome = SimulatedPaymentOutcome.Failed;
            return true;
        }

        outcome = default;
        return false;
    }

    internal SimulatedPaymentOutcome GetOutcome()
    {
        if (TryGetOutcome(out var outcome))
        {
            return outcome;
        }

        throw new InvalidOperationException(
            $"Unsupported simulated payment outcome: {SimulatedOutcome}.");
    }
}
