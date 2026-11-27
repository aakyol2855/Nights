using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nights.Models.Entities;

namespace Nights.Data.Configurations;

public class GameScoreConfiguration : IEntityTypeConfiguration<GameScore>
{
    public void Configure(EntityTypeBuilder<GameScore> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.GameSlug).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Difficulty).HasMaxLength(32).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.PlayerDisplayName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // Kullanıcı başına oyun+zorluk bazlı hızlı en iyi skor sorgusu indeksi
        builder.HasIndex(e => new { e.UserId, e.GameSlug, e.Difficulty, e.IsDeleted });

        // Top 10 liderlik tablosu SQL sıralama indeksi
        builder.HasIndex(e => new { e.GameSlug, e.Difficulty, e.IsDeleted, e.Score, e.DurationSeconds });
    }
}
