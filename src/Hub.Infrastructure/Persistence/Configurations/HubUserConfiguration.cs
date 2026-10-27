using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Hub.Infrastructure.Persistence.Configurations;

public sealed class HubUserConfiguration : IEntityTypeConfiguration<HubUser>
{
    public void Configure(EntityTypeBuilder<HubUser> builder)
    {
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Email).HasMaxLength(254);
    }
}
