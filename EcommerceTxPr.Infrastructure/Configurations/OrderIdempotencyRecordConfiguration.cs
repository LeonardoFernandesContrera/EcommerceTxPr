using EcommerceTxPr.Application.Orders.Idempotency;
using EcommerceTxPr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class OrderIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<OrderIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<OrderIdempotencyRecord> builder)
    {
        builder.ToTable("OrderIdempotencyRecords");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id)
            .ValueGeneratedNever();

        builder.Property(record => record.KeyHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(record => record.KeyHash)
            .HasDatabaseName("UX_OrderIdempotencyRecords_KeyHash")
            .IsUnique();

        builder.Property(record => record.RequestHash)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsUnicode(false)
            .IsRequired();

        builder.Property(record => record.OrderId)
            .IsRequired();

        builder.HasIndex(record => record.OrderId);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(record => record.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(record => record.CreationDate)
            .IsRequired();
    }
}
