using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(36)
            .IsConcurrencyToken();

        builder.Property(x => x.SecurityStamp)
            .HasMaxLength(32);

        builder.Property(x => x.UserName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.NormalizedUserName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(256);

        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();

        builder.HasMany<IdentityUserClaim<string>>()
            .WithOne()
            .HasForeignKey(uc => uc.UserId)
            .IsRequired();

        builder.HasMany<IdentityUserLogin<string>>()
            .WithOne()
            .HasForeignKey(ul => ul.UserId)
            .IsRequired();

        builder.HasMany<IdentityUserToken<string>>()
            .WithOne()
            .HasForeignKey(ut => ut.UserId)
            .IsRequired();

        builder.HasMany<IdentityUserPasskey<string>>()
            .WithOne()
            .HasForeignKey(up => up.UserId)
            .IsRequired();
    }
}