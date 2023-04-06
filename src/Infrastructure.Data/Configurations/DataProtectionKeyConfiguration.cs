using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Infrastructure.Data.Configurations;

internal class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.Property(x => x.Xml)
            .HasColumnType("text");

        builder.Property(x => x.FriendlyName)
            .HasColumnType("text");
    }
}