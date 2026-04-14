using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder)
    {
        builder.ToTable("user_logins");

        builder.Property(x => x.UserId)
            .HasColumnType("uuid");

        builder.Property(x => x.LoginProvider)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(256);

        builder.Property(x => x.ProviderDisplayName)
            .HasMaxLength(128);
    }
}