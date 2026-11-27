using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nights.Models.Entities;

namespace Nights.Data.Configurations;

public class SystemAuditLogConfiguration : IEntityTypeConfiguration<SystemAuditLog>
{
    public void Configure(EntityTypeBuilder<SystemAuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Category).HasMaxLength(80).IsRequired();
        builder.Property(e => e.LogLevel).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Username).HasMaxLength(150).IsRequired();
        builder.Property(e => e.TargetEntityType).HasMaxLength(60);
        builder.Property(e => e.TargetEntityId).HasMaxLength(80);
        builder.Property(e => e.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Component).HasMaxLength(80).IsRequired();

        builder.HasIndex(e => e.TimestampUtc);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.LogLevel);
    }
}
