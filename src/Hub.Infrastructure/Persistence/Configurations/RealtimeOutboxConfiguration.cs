using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hub.Infrastructure.Persistence.Configurations;

public sealed class RealtimeOutboxConfiguration : IEntityTypeConfiguration<RealtimeOutboxEntry>
{
    public void Configure(EntityTypeBuilder<RealtimeOutboxEntry> builder)
    {
        builder.ToTable("RealtimeOutbox");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Topic).HasMaxLength(160);
        builder.HasIndex(entry => entry.CreatedAt);
    }
}
