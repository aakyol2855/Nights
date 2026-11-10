using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nights.Models.Entities;

namespace Nights.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.AuthorName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

        // Performans ve Sıralama İndeksleri
        // IsDeleted filtresi WHERE koşulunda sık kullanıldığından composite indeks
        builder.HasIndex(e => new { e.IsDeleted, e.IsPinned, e.CreatedAt })
            .HasDatabaseName("IX_Notes_IsDeleted_IsPinned_CreatedAt");
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_Notes_CreatedAt");
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_Notes_UserId");
    }
}
