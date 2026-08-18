using EcommerceTxPr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.Property(product => product.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(product => product.Sku)
            .IsUnique();

        builder.Property(product => product.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .IsRequired();
    }
}
