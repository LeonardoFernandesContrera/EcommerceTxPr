using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Infrastructure.Inbox;
using EcommerceTxPr.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.IntegrationTests.Infrastructure;

internal sealed class SqliteDatabaseErrorClassifier
    : IDatabaseErrorClassifier
{
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintPrimaryKey = 1555;

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

    public bool IsInboxConflict(DbUpdateException exception)
    {
        var hasInboxEntry = exception.Entries.Any(
            entry => entry.Entity is InboxMessage);

        return hasInboxEntry
            && exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteExtendedErrorCode
                is SqliteConstraintPrimaryKey or SqliteConstraintUnique;
    }
}
