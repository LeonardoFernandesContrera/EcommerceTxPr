using EcommerceTxPr.Application.Orders.Idempotency;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Persistence;

public sealed class SqlServerDatabaseErrorClassifier
    : IDatabaseErrorClassifier
{
    public bool IsIdempotencyConflict(DbUpdateException exception)
    {
        var hasIdempotencyEntry = exception.Entries.Any(
            entry => entry.Entity is OrderIdempotencyRecord);

        return hasIdempotencyEntry
            && exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }
}
