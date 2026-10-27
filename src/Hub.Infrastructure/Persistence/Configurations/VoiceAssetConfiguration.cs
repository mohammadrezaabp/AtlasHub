using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Hub.Infrastructure.Persistence.Configurations;

public sealed class VoiceAssetConfiguration : IEntityTypeConfiguration<VoiceAsset>
{
    public void Configure(EntityTypeBuilder<VoiceAsset> builder)
    {
        builder.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
