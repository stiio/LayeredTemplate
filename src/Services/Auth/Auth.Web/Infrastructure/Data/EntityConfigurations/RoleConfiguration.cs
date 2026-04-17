using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class RoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles");

        builder.Property(x => x.Id)
            .HasColumnType("uuid")
            .HasConversion<Guid>();

        builder.Property(x => x.Name)
            .HasMaxLength(32);

        builder.Property(x => x.NormalizedName)
            .HasMaxLength(32);

        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(36);
    }
}