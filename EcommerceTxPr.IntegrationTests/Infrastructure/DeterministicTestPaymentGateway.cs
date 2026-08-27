using EcommerceTxPr.Application.Payments.Gateways;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed class DeterministicTestPaymentGateway : IPaymentGateway
{
    private readonly object _sync = new();
    private readonly Dictionary<string, StoredOperation> _operations = new(
        StringComparer.Ordinal);
    private readonly Queue<PaymentGatewayResult> _observations = new();
    private readonly List<PaymentGatewayRequest> _requests = new();
    private PaymentGatewayResult _result =
        PaymentGatewayResult.Succeeded("test-provider-reference");

    public PaymentGatewayResult Result
    {
        get
        {
            lock (_sync)
            {
                return _result;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            lock (_sync)
            {
                _result = value;
            }
        }
    }

    public IReadOnlyList<PaymentGatewayRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return _requests.ToArray();
            }
        }
    }

    public int GatewayRequestCount
    {
        get
        {
            lock (_sync)
            {
                return _requests.Count;
            }
        }
    }

    public int ExternalEffectExecutionCount { get; private set; }

    public Func<
        PaymentGatewayRequest,
        CancellationToken,
        Task>? BeforeReturningAsync { get; set; }

    public void EnqueueObservation(PaymentGatewayResult observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        lock (_sync)
        {
            _observations.Enqueue(observation);
        }
    }

    public async Task<PaymentGatewayResult> ProcessAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        PaymentGatewayResult observedResult;

        lock (_sync)
        {
            _requests.Add(request);

            if (_operations.TryGetValue(
                    request.IdempotencyKey,
                    out var existing))
            {
                EnsureEquivalent(existing, request);
                observedResult = Observe(existing.Result);
            }
            else
            {
                var operation = new StoredOperation(
                    request.PaymentId,
                    request.Amount,
                    _result);
                _operations.Add(request.IdempotencyKey, operation);
                ExternalEffectExecutionCount++;
                observedResult = Observe(operation.Result);
            }
        }

        if (BeforeReturningAsync is not null)
        {
            await BeforeReturningAsync(request, cancellationToken);
        }

        return observedResult;
    }

    private PaymentGatewayResult Observe(PaymentGatewayResult storedResult)
    {
        return _observations.Count > 0
            ? _observations.Dequeue()
            : storedResult;
    }

    private static void EnsureEquivalent(
        StoredOperation existing,
        PaymentGatewayRequest request)
    {
        if (existing.PaymentId != request.PaymentId
            || existing.Amount != request.Amount)
        {
            throw new InvalidOperationException(
                "A provider idempotency key was reused for another payment "
                + "request.");
        }
    }

    private sealed record StoredOperation(
        Guid PaymentId,
        decimal Amount,
        PaymentGatewayResult Result);
}
