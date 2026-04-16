using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class UserPasskeyConfiguration : IEntityTypeConfiguration<IdentityUserPasskey<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserPasskey<string>> builder)
    {
        builder.ToTable("user_passkeys");

        builder.HasKey(x => x.CredentialId);

        builder.Property(x => x.CredentialId)
            .HasMaxLength(1024);

        builder.Property(x => x.UserId)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.OwnsOne(p => p.Data).ToJson();
    }
}