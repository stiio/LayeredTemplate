using LayeredTemplate.App.Features.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.App.Features.Users.DbConfig;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Phone).HasMaxLength(32);
        builder.Property(u => u.SecurityStamp).HasMaxLength(32);
    }
}
