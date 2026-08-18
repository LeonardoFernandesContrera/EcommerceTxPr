using EcommerceTxPr.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(customer => customer.Name)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(customer => customer.BirthDate)
            .IsRequired();

        builder.Property(customer => customer.IsActive)
            .IsRequired();
    }
}
