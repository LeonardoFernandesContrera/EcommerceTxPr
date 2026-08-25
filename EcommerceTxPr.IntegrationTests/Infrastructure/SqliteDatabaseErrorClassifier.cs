using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed class SqliteDatabaseErrorClassifier
    : IDatabaseErrorClassifier
{
    private const int SqliteConstraintUnique = 2067;

    public bool IsIdempotencyConflict(DbUpdateException exception)
    {
        var hasIdempotencyEntry = exception.Entries.Any(
            entry => entry.Entity is OrderIdempotencyRecord);

        return hasIdempotencyEntry
            && exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteConstraintUnique;
    }
}
