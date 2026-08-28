using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Queue<SaveChangesResult> _results = new();

    public SaveChangesResult Result { get; set; } = SaveChangesResult.Success;

    public int SaveChangesCalls { get; private set; }

    public Action<int>? OnSaveChanges { get; set; }

    public void EnqueueResult(SaveChangesResult result)
    {
        _results.Enqueue(result);
    }

    public Task<SaveChangesResult> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        OnSaveChanges?.Invoke(SaveChangesCalls);
        var result = _results.Count > 0 ? _results.Dequeue() : Result;
        return Task.FromResult(result);
    }
}
