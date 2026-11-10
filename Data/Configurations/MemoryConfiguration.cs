using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nights.Models.Entities;

namespace Nights.Data.Configurations;

public class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).HasMaxLength(280).IsRequired();
        builder.Property(e => e.ImageFileName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SubmitterIpHash).HasMaxLength(64);
        builder.HasIndex(e => new { e.IsApproved, e.CreatedAt });
    }
}
