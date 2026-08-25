using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
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

    public bool IsPaymentConflict(DbUpdateException exception)
    {
        var hasPaymentEntry = exception.Entries.Any(
            entry => entry.Entity is Payment);

        return hasPaymentEntry
            && exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                == SqliteConstraintUnique;
    }
}
