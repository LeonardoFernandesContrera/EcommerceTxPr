using EcommerceTxPr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.Property(order => order.CustomerId)
            .IsRequired();

        builder.HasIndex(order => order.CustomerId);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Ignore(order => order.Total);

        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
