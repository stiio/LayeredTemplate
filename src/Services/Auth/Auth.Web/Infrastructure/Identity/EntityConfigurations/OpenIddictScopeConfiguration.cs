using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class OpenIddictScopeConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreScope>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreScope> builder)
    {
        builder.ToTable("openiddict_scopes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.Descriptions)
            .HasColumnType("jsonb");

        builder.Property(x => x.DisplayNames)
            .HasColumnType("jsonb");

        builder.Property(x => x.Properties)
            .HasColumnType("jsonb");

        builder.Property(x => x.Resources)
            .HasColumnType("jsonb");
    }
}