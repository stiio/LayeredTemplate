using LayeredTemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Infrastructure.Data.Configurations;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.Phone)
            .HasMaxLength(30);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}