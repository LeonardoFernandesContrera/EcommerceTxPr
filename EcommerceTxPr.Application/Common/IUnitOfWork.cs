namespace EcommerceTxPr.Application.Common;

public interface IUnitOfWork
{
    Task<SaveChangesResult> SaveChangesAsync(
        CancellationToken cancellationToken);
}
