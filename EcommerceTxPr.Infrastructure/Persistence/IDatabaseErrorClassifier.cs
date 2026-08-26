using Microsoft.EntityFrameworkCore;

namespace EcommerceTxPr.Infrastructure.Persistence;

public interface IDatabaseErrorClassifier
{
    bool IsIdempotencyConflict(DbUpdateException exception);

    bool IsPaymentConflict(DbUpdateException exception);

    bool IsInboxConflict(DbUpdateException exception);
}
