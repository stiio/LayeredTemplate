using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class OpenIddictScopeConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreScope>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreScope> builder)
    {
        builder.ToTable("openiddict_scopes");

        builder.Property(x => x.Id).HasColumnType("uuid").HasConversion<Guid>();
        builder.Property(x => x.Descriptions).HasColumnType("text");
        builder.Property(x => x.DisplayNames).HasColumnType("text");
        builder.Property(x => x.Properties).HasColumnType("text");
        builder.Property(x => x.Resources).HasColumnType("text");
        builder.Property(x => x.Description).HasColumnType("text");
    }
}
