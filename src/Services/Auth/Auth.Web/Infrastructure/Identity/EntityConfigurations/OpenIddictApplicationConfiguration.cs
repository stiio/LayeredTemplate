using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictApplicationConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreApplication>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreApplication> builder)
    {
        builder.ToTable("openiddict_application");

        builder.Property(x => x.Id).HasColumnType("uuid").HasConversion<Guid>();
        builder.Property(x => x.Permissions).HasColumnType("text");
        builder.Property(x => x.RedirectUris).HasColumnType("text");
        builder.Property(x => x.PostLogoutRedirectUris).HasColumnType("text");
        builder.Property(x => x.Properties).HasColumnType("text");
        builder.Property(x => x.Requirements).HasColumnType("text");
        builder.Property(x => x.Settings).HasColumnType("text");
        builder.Property(x => x.JsonWebKeySet).HasColumnType("text");
        builder.Property(x => x.DisplayNames).HasColumnType("text");
        builder.Property(x => x.ClientSecret).HasColumnType("text");
    }
}
