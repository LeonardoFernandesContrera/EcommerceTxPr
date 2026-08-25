namespace EcommerceTxPr.Application.Common;

public enum SaveChangesResult
{
    Success = 0,
    ConcurrencyConflict = 1,
    IdempotencyConflict = 2
}
