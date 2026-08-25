using System.Security.Cryptography;
using System.Text;
using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.Application.Orders.Idempotency;

public sealed class OrderIdempotencyKey
{
    public const int MaxLength = 100;

    private OrderIdempotencyKey(string keyHash)
    {
        KeyHash = keyHash;
    }

    public string KeyHash { get; }

    public static Result<OrderIdempotencyKey, Error> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<OrderIdempotencyKey, Error>.Failure(
                OrderErrors.IdempotencyKeyRequired);
        }

        if (value.Length > MaxLength)
        {
            return Result<OrderIdempotencyKey, Error>.Failure(
                OrderErrors.IdempotencyKeyTooLong);
        }

        var keyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        return Result<OrderIdempotencyKey, Error>.Success(
            new OrderIdempotencyKey(keyHash));
    }
}
