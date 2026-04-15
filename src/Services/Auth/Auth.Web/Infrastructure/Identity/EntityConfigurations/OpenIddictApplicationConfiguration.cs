using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictApplicationConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreApplication>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreApplication> builder)
    {
        builder.ToTable("openiddict_application");

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.Permissions)
            .HasColumnType("jsonb");

        builder.Property(x => x.RedirectUris)
            .HasColumnType("jsonb");

        builder.Property(x => x.Properties)
            .HasColumnType("jsonb");

        builder.Property(x => x.PostLogoutRedirectUris)
            .HasColumnType("jsonb");

        builder.Property(x => x.Requirements)
            .HasColumnType("jsonb");

        builder.Property(x => x.Settings)
            .HasColumnType("jsonb");

        builder.Property(x => x.JsonWebKeySet)
            .HasColumnType("jsonb");
    }
}