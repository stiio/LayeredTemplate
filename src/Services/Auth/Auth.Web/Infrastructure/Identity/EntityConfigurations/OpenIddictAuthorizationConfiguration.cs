using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictAuthorizationConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreAuthorization>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreAuthorization> builder)
    {
        builder.ToTable("openiddict_authorization");

        builder.Property(x => x.Id).HasColumnType("uuid").HasConversion<Guid>();
        builder.Property(x => x.Properties).HasColumnType("text");
        builder.Property(x => x.Scopes).HasColumnType("text");
    }
}
