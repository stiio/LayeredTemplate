using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder)
    {
        builder.ToTable("user_claims");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClaimType)
            .HasMaxLength(128);

        builder.Property(x => x.ClaimValue)
            .HasMaxLength(256);
    }
}