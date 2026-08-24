using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.UnitTests.TestDoubles;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public SaveChangesResult Result { get; set; } = SaveChangesResult.Success;

    public int SaveChangesCalls { get; private set; }

    public Task<SaveChangesResult> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.FromResult(Result);
    }
}
