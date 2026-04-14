using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Identity.EntityConfigurations;

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> builder)
    {
        builder.ToTable("roles", t => t.ExcludeFromMigrations());

        builder.Property(x => x.Id)
            .HasColumnType("uuid");

        builder.Property(x => x.Name)
            .HasMaxLength(32);

        builder.Property(x => x.NormalizedName)
            .HasMaxLength(32);

        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(32);
    }
}