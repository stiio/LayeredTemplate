using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictTokenConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreToken>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreToken> builder)
    {
        builder.ToTable("openiddict_token");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        /*
        builder.Property("application_id")
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property("authorization_id")
            .HasColumnType("uuid")
            .HasConversion<Guid>();
        */

        builder.Property(x => x.Payload)
            .HasColumnType("text");

        builder.Property(x => x.Properties)
            .HasColumnType("jsonb");
    }
}