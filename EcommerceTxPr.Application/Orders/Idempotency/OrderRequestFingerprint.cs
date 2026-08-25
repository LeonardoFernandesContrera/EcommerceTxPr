using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcommerceTxPr.Application.Common;
using EcommerceTxPr.Application.Orders.Contracts;

namespace EcommerceTxPr.Application.Orders.Idempotency;

public static class OrderRequestFingerprint
{
    public static Result<NormalizedOrderRequest, Error> Create(
        CreateOrderRequest request)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result<NormalizedOrderRequest, Error>.Failure(
                OrderErrors.InvalidCustomer);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Result<NormalizedOrderRequest, Error>.Failure(
                OrderErrors.Empty);
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty))
        {
            return Result<NormalizedOrderRequest, Error>.Failure(
                OrderErrors.InvalidProduct);
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Result<NormalizedOrderRequest, Error>.Failure(
                OrderErrors.InvalidQuantity);
        }

        var quantities = new Dictionary<Guid, int>();

        try
        {
            foreach (var item in request.Items)
            {
                quantities[item.ProductId] = quantities.TryGetValue(
                    item.ProductId,
                    out var currentQuantity)
                    ? checked(currentQuantity + item.Quantity)
                    : item.Quantity;
            }
        }
        catch (OverflowException)
        {
            return Result<NormalizedOrderRequest, Error>.Failure(
                OrderErrors.InvalidQuantity);
        }

        var items = quantities
            .Select(pair => new NormalizedOrderItem(pair.Key, pair.Value))
            .OrderBy(
                item => item.ProductId.ToString("N"),
                StringComparer.Ordinal)
            .ToArray();
        var canonicalValue = BuildCanonicalValue(request.CustomerId, items);
        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue)));

        return Result<NormalizedOrderRequest, Error>.Success(
            new NormalizedOrderRequest(request.CustomerId, items, requestHash));
    }

    private static string BuildCanonicalValue(
        Guid customerId,
        IReadOnlyCollection<NormalizedOrderItem> items)
    {
        var builder = new StringBuilder()
            .Append("customer:")
            .Append(customerId.ToString("N"))
            .Append('\n');

        foreach (var item in items)
        {
            builder
                .Append("item:")
                .Append(item.ProductId.ToString("N"))
                .Append(':')
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return builder.ToString();
    }
}
