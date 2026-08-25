using EcommerceTxPr.Domain.Entities;
using EcommerceTxPr.Domain.Enums;

namespace EcommerceTxPr.UnitTests.Domain;

public sealed class PaymentTests
{
    [Fact]
    public void Constructor_creates_pending_payment_with_stable_identity()
    {
        var beforeCreation = DateTime.UtcNow;
        var orderId = Guid.NewGuid();

        var payment = new Payment(orderId, 125.50m);

        var afterCreation = DateTime.UtcNow;
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(orderId, payment.OrderId);
        Assert.Equal(125.50m, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.ProviderReference);
        Assert.Null(payment.FailureCode);
        Assert.Equal(DateTimeKind.Utc, payment.CreationDate.Kind);
        Assert.InRange(payment.CreationDate, beforeCreation, afterCreation);
    }

    [Fact]
    public void Constructor_rejects_empty_order_id()
    {
        Assert.Throws<ArgumentException>(() => new Payment(Guid.Empty, 10m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Payment(Guid.NewGuid(), amount));
    }

    [Fact]
    public void MarkSucceeded_completes_pending_payment_without_changing_identity()
    {
        var payment = new Payment(Guid.NewGuid(), 10m);
        var originalId = payment.Id;
        var originalCreationDate = payment.CreationDate;

        payment.MarkSucceeded("provider-reference");

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("provider-reference", payment.ProviderReference);
        Assert.Null(payment.FailureCode);
        Assert.Equal(originalId, payment.Id);
        Assert.Equal(originalCreationDate, payment.CreationDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkSucceeded_rejects_blank_provider_reference(string? value)
    {
        var payment = new Payment(Guid.NewGuid(), 10m);

        Assert.Throws<ArgumentException>(
            () => payment.MarkSucceeded(value!));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void MarkFailed_completes_pending_payment_without_changing_identity()
    {
        var payment = new Payment(Guid.NewGuid(), 10m);
        var originalId = payment.Id;
        var originalCreationDate = payment.CreationDate;

        payment.MarkFailed("Declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("Declined", payment.FailureCode);
        Assert.Null(payment.ProviderReference);
        Assert.Equal(originalId, payment.Id);
        Assert.Equal(originalCreationDate, payment.CreationDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkFailed_rejects_blank_failure_code(string? value)
    {
        var payment = new Payment(Guid.NewGuid(), 10m);

        Assert.Throws<ArgumentException>(() => payment.MarkFailed(value!));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Completed_payment_rejects_every_further_transition()
    {
        var succeeded = new Payment(Guid.NewGuid(), 10m);
        succeeded.MarkSucceeded("reference");
        var failed = new Payment(Guid.NewGuid(), 10m);
        failed.MarkFailed("Declined");

        Assert.Throws<InvalidOperationException>(
            () => succeeded.MarkSucceeded("another-reference"));
        Assert.Throws<InvalidOperationException>(
            () => succeeded.MarkFailed("Declined"));
        Assert.Throws<InvalidOperationException>(
            () => failed.MarkSucceeded("reference"));
        Assert.Throws<InvalidOperationException>(
            () => failed.MarkFailed("AnotherFailure"));
        Assert.Equal("reference", succeeded.ProviderReference);
        Assert.Null(succeeded.FailureCode);
        Assert.Equal("Declined", failed.FailureCode);
        Assert.Null(failed.ProviderReference);
    }
}
