using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder)
    {
        builder.ToTable("user_tokens");

        builder.Property(x => x.UserId)
            .HasColumnType("uuid");

        builder.Property(x => x.LoginProvider)
            .HasMaxLength(128);

        builder.Property(x => x.Name)
            .HasMaxLength(128);

        builder.Property(x => x.Value)
            .HasMaxLength(256);
    }
}