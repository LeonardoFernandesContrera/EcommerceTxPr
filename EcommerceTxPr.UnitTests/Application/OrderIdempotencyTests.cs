using EcommerceTxPr.Application.Orders;
using EcommerceTxPr.Application.Orders.Contracts;
using EcommerceTxPr.Application.Orders.Idempotency;

namespace EcommerceTxPr.UnitTests.Application;

public sealed class OrderIdempotencyTests
{
    private static readonly Guid CustomerId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111");

    private static readonly Guid FirstProductId = Guid.Parse(
        "22222222-2222-2222-2222-222222222222");

    private static readonly Guid SecondProductId = Guid.Parse(
        "33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Key_create_hashes_exact_utf8_value_to_fixed_ascii_fingerprint()
    {
        var result = OrderIdempotencyKey.Create("CaseSensitive");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "ACC7AD4051305A22DBDE2D6B458021DAC68B64B5800B25AE427E913E631825FB",
            result.Value?.KeyHash);
    }

    [Fact]
    public void Key_create_uses_case_sensitive_comparison_semantics()
    {
        var upper = OrderIdempotencyKey.Create("ORDER-KEY");
        var lower = OrderIdempotencyKey.Create("order-key");

        Assert.True(upper.IsSuccess);
        Assert.True(lower.IsSuccess);
        Assert.NotEqual(upper.Value?.KeyHash, lower.Value?.KeyHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_create_rejects_missing_or_blank_value(string? value)
    {
        var result = OrderIdempotencyKey.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.IdempotencyKeyRequired, result.Error);
    }

    [Fact]
    public void Key_create_rejects_value_over_100_characters()
    {
        var result = OrderIdempotencyKey.Create(new string('a', 101));

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.IdempotencyKeyTooLong, result.Error);
    }

    [Fact]
    public void Request_fingerprint_has_stable_canonical_hash_and_aggregated_items()
    {
        var request = new CreateOrderRequest(
            CustomerId,
            new[]
            {
                new CreateOrderItemRequest(SecondProductId, 1),
                new CreateOrderItemRequest(FirstProductId, 2),
                new CreateOrderItemRequest(FirstProductId, 3)
            });

        var result = OrderRequestFingerprint.Create(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "59AF244BF72F8F09E19B67AAC1E5445A997CC65F0C2941A403D424CD0096316E",
            result.Value?.RequestHash);
        Assert.Collection(
            result.Value!.Items,
            item =>
            {
                Assert.Equal(FirstProductId, item.ProductId);
                Assert.Equal(5, item.Quantity);
            },
            item =>
            {
                Assert.Equal(SecondProductId, item.ProductId);
                Assert.Equal(1, item.Quantity);
            });
    }

    [Fact]
    public void Request_fingerprint_ignores_item_order_and_duplicate_splitting()
    {
        var splitAndUnordered = new CreateOrderRequest(
            CustomerId,
            new[]
            {
                new CreateOrderItemRequest(SecondProductId, 1),
                new CreateOrderItemRequest(FirstProductId, 2),
                new CreateOrderItemRequest(FirstProductId, 3)
            });
        var aggregatedAndOrdered = new CreateOrderRequest(
            CustomerId,
            new[]
            {
                new CreateOrderItemRequest(FirstProductId, 5),
                new CreateOrderItemRequest(SecondProductId, 1)
            });

        var first = OrderRequestFingerprint.Create(splitAndUnordered);
        var second = OrderRequestFingerprint.Create(aggregatedAndOrdered);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value?.RequestHash, second.Value?.RequestHash);
    }

    [Fact]
    public void Request_fingerprint_changes_when_customer_or_quantity_changes()
    {
        var original = OrderRequestFingerprint.Create(new CreateOrderRequest(
            CustomerId,
            new[] { new CreateOrderItemRequest(FirstProductId, 1) }));
        var anotherCustomer = OrderRequestFingerprint.Create(new CreateOrderRequest(
            Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"),
            new[] { new CreateOrderItemRequest(FirstProductId, 1) }));
        var anotherQuantity = OrderRequestFingerprint.Create(new CreateOrderRequest(
            CustomerId,
            new[] { new CreateOrderItemRequest(FirstProductId, 2) }));

        Assert.NotEqual(
            original.Value?.RequestHash,
            anotherCustomer.Value?.RequestHash);
        Assert.NotEqual(
            original.Value?.RequestHash,
            anotherQuantity.Value?.RequestHash);
    }

    [Fact]
    public void Request_fingerprint_rejects_duplicate_quantity_overflow()
    {
        var request = new CreateOrderRequest(
            CustomerId,
            new[]
            {
                new CreateOrderItemRequest(FirstProductId, int.MaxValue),
                new CreateOrderItemRequest(FirstProductId, 1)
            });

        var result = OrderRequestFingerprint.Create(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
    }
}
