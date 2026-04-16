using LayeredTemplate.Auth.Web.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LayeredTemplate.Auth.Web.Infrastructure.Data.EntityConfigurations;

public class SigningCredentialConfiguration : IEntityTypeConfiguration<SigningCredential>
{
    public void Configure(EntityTypeBuilder<SigningCredential> builder)
    {
        builder.ToTable("signing_credentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.KeyData).HasColumnType("text").IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
