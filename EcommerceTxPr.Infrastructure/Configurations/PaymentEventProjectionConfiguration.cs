using EcommerceTxPr.Infrastructure.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class PaymentEventProjectionConfiguration
    : IEntityTypeConfiguration<PaymentEventProjection>
{
    public void Configure(EntityTypeBuilder<PaymentEventProjection> builder)
    {
        builder.ToTable("PaymentEventProjections");

        builder.HasKey(projection => projection.MessageId);

        builder.Property(projection => projection.MessageId)
            .ValueGeneratedNever();

        builder.HasOne<InboxMessage>()
            .WithOne()
            .HasForeignKey<PaymentEventProjection>(
                projection => projection.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(projection => projection.PaymentId)
            .IsRequired();

        builder.Property(projection => projection.OrderId)
            .IsRequired();

        builder.Property(projection => projection.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(projection => projection.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(projection => projection.OccurredOnUtc)
            .IsRequired();

        builder.Property(projection => projection.ProcessedOnUtc)
            .IsRequired();

        builder.Property(projection => projection.ProviderReference)
            .HasMaxLength(PaymentEventProjection.MaxProviderReferenceLength);

        builder.Property(projection => projection.FailureCode)
            .HasMaxLength(PaymentEventProjection.MaxFailureCodeLength);
    }
}
