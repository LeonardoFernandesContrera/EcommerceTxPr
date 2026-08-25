using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
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

    public bool IsPaymentConflict(DbUpdateException exception)
    {
        var hasPaymentEntry = exception.Entries.Any(
            entry => entry.Entity is Payment);

        return hasPaymentEntry
            && exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }
}
