using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<string>> builder)
    {
        builder.ToTable("role_claims");

        builder.Property(x => x.RoleId)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.ClaimType)
            .HasMaxLength(128);

        builder.Property(x => x.ClaimValue)
            .HasMaxLength(256);
    }
}