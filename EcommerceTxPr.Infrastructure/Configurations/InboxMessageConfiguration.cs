using EcommerceTxPr.Infrastructure.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcommerceTxPr.Infrastructure.Configurations;

public sealed class InboxMessageConfiguration
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");

        builder.HasKey(message => message.MessageId);

        builder.Property(message => message.MessageId)
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(message => message.ProcessedOnUtc)
            .IsRequired();
    }
}
