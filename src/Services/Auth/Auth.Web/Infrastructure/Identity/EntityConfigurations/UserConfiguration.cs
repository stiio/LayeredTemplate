using LayeredTemplate.Auth.Web.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(x => x.Id)
            .HasColumnType("uuid");

        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(32);

        builder.Property(x => x.SecurityStamp)
            .HasMaxLength(32);

        builder.Property(x => x.UserName)
            .HasMaxLength(128);

        builder.Property(x => x.NormalizedUserName)
            .HasMaxLength(128);

        builder.Property(x => x.Email)
            .HasMaxLength(128);

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(128);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(256);
    }
}