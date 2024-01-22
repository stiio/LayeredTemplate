using LayeredTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Infrastructure.Data.Configurations;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.Phone)
            .HasMaxLength(32);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(user => user.SecurityStamp)
            .HasMaxLength(32);
    }
}