using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class OpenIddictTokenConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreToken>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreToken> builder)
    {
        builder.ToTable("openiddict_token");

        builder.Property(x => x.Id).HasColumnType("uuid").HasConversion<Guid>();
        builder.Property(x => x.Payload).HasColumnType("text");
        builder.Property(x => x.Properties).HasColumnType("text");
    }
}
