using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder)
    {
        builder.ToTable("user_logins");

        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });

        builder.Property(x => x.UserId)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.LoginProvider)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(128);

        builder.Property(x => x.ProviderDisplayName)
            .HasMaxLength(128);
    }
}