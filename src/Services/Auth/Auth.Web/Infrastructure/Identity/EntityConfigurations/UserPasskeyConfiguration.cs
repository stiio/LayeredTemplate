using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserPasskeyConfiguration : IEntityTypeConfiguration<IdentityUserPasskey<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserPasskey<string>> builder)
    {
        builder.ToTable("user_passkeys", t => t.ExcludeFromMigrations());

        builder.Property(x => x.UserId)
            .HasColumnType("uuid");
    }
}