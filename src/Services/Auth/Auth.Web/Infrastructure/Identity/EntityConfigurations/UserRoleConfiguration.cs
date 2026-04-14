using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<string>> builder)
    {
        builder.ToTable("user_roles", t => t.ExcludeFromMigrations());

        builder.Property(x => x.RoleId)
            .HasColumnType("uuid");

        builder.Property(x => x.UserId)
            .HasColumnType("uuid");
    }
}