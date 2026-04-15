using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictAuthorizationConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreAuthorization>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreAuthorization> builder)
    {
        builder.ToTable("openiddict_authorization");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        /*
        builder.Property("application_id")
            .HasColumnType("uuid")
            .HasConversion<Guid>();
        */

        builder.Property(x => x.Properties)
            .HasColumnType("jsonb");

        builder.Property(x => x.Scopes)
            .HasColumnType("jsonb");
    }
}