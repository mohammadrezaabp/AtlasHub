using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Hub.Infrastructure.Persistence.Configurations;

public sealed class ModelUsageConfiguration : IEntityTypeConfiguration<ModelUsage>
{
    public void Configure(EntityTypeBuilder<ModelUsage> builder)
    {
        builder.Property(x => x.CostUsd).HasPrecision(18, 9);
        builder.HasIndex(x => new { x.Actor, x.CreatedAt });
    }
}
